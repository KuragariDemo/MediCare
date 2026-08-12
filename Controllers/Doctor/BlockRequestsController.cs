// Controllers/Doctor/BlockRequestsController.cs
using MediCare.App.Data;
using MediCare.App.Models;
using MediCare.App.Models.Scheduling;
using MediCare.ViewModels.Schedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MediCare.App.Controllers.Doctor
{
    [Authorize(Roles = "Doctor")]
    [Route("Doctors/[controller]")]
    public class BlockRequestsController : Controller
    {
        private readonly MediCareContext _db;

        public BlockRequestsController(MediCareContext db) { _db = db; }

        private string CurrentUserId =>
            User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

        private static DateTime ToLocal(DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(
#if WINDOWS
                "SE Asia Standard Time" // Thailand time for your env
#else
                "Asia/Bangkok"
#endif
            ));

        private static DateTime ToUtc(DateTime local)
            => TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById(
#if WINDOWS
                "SE Asia Standard Time"
#else
                "Asia/Bangkok"
#endif
            ));

        // ---------- LIST ----------
        // GET /Doctors/BlockRequests/List?status=all|pending|approved|rejected|cancelled
        [HttpGet("List")]
        public async Task<IActionResult> List([FromQuery] string status = "all")
        {
            var me = CurrentUserId;

            IQueryable<DoctorBlockRequest> q = _db.DoctorBlockRequests
                .AsNoTracking()
                .Where(x => x.DoctorId == me);

            if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<BlockRequestStatus>(status, true, out var s))
                    q = q.Where(x => x.Status == s);
                else
                    return BadRequest("Invalid status.");
            }

            // Apply ordering once, at the end
            q = q.OrderByDescending(x => x.RequestedAtUtc);

            var rows = await q.Select(x => new LeaveRequestDto
            {
                Id = x.Id,
                DoctorId = x.DoctorId,
                Status = x.Status.ToString().ToLower(),
                Start = ToLocal(x.StartDateUtc).ToString("yyyy-MM-dd"),
                End = ToLocal(x.EndDateUtc).ToString("yyyy-MM-dd"),
                Submitted = ToLocal(x.RequestedAtUtc).ToString("yyyy-MM-dd"),
                Reason = x.Reason
            }).ToListAsync();

            return Ok(rows);
        }

        // ---------- DETAILS ----------
        // GET /Doctors/BlockRequests/Details/5
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details([FromRoute] int id)
        {
            var me = CurrentUserId;
            var x = await _db.DoctorBlockRequests.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == id && r.DoctorId == me);
            if (x == null) return NotFound();

            var dto = new LeaveRequestDto
            {
                Id = x.Id,
                DoctorId = x.DoctorId,
                Status = x.Status.ToString().ToLower(),
                Start = ToLocal(x.StartDateUtc).ToString("yyyy-MM-dd"),
                End = ToLocal(x.EndDateUtc).ToString("yyyy-MM-dd"),
                Submitted = ToLocal(x.RequestedAtUtc).ToString("yyyy-MM-dd"),
                Reason = x.Reason
            };
            return Ok(dto);
        }

        // ---------- CREATE ----------
        // POST /Doctors/BlockRequests/Create  (JSON body)
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] LeaveRequestCreateVm vm)
        {
            if (!ModelState.IsValid) return BadRequest("Invalid data.");
            var me = CurrentUserId;

            // security: enforce ownership
            var doctorId = string.IsNullOrWhiteSpace(vm.DoctorId) ? me : vm.DoctorId;
            if (doctorId != me) return Forbid();

            // validate dates
            var startLocal = vm.StartDate.Date;
            var endLocal = vm.EndDate.Date;
            if (startLocal > endLocal) return BadRequest("Start date cannot be after end date.");

            // optional: prevent overlaps with existing Approved
            var startUtc = ToUtc(startLocal);
            var endUtc = ToUtc(endLocal.AddDays(1).AddSeconds(-1)); // inclusive end of day

            bool overlapsApproved = await _db.DoctorBlockRequests
                .AnyAsync(r => r.DoctorId == me &&
                               r.Status == BlockRequestStatus.Approved &&
                               r.StartDateUtc <= endUtc && startUtc <= r.EndDateUtc);
            if (overlapsApproved)
                return BadRequest("Overlaps with existing approved block.");

            bool overlapsPending = await _db.DoctorBlockRequests
                .AnyAsync(r => r.DoctorId == me &&
                               r.Status == BlockRequestStatus.Pending &&
                               r.StartDateUtc <= endUtc && startUtc <= r.EndDateUtc);
            if (overlapsPending)
                return BadRequest("You already have a pending request that overlaps this period.");

            var req = new DoctorBlockRequest
            {
                DoctorId = me,
                StartDateUtc = startUtc,
                EndDateUtc = endUtc,
                Reason = string.IsNullOrWhiteSpace(vm.Reason) ? null : vm.Reason!.Trim(),
                Status = BlockRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            };

            _db.DoctorBlockRequests.Add(req);
            await _db.SaveChangesAsync();

            var dto = new LeaveRequestDto
            {
                Id = req.Id,
                DoctorId = req.DoctorId,
                Status = req.Status.ToString().ToLower(),
                Start = startLocal.ToString("yyyy-MM-dd"),
                End = endLocal.ToString("yyyy-MM-dd"),
                Submitted = ToLocal(req.RequestedAtUtc).ToString("yyyy-MM-dd"),
                Reason = req.Reason
            };
            return Ok(dto);
        }

        // ---------- CANCEL ----------
        // POST /Doctors/BlockRequests/Cancel/5
        [HttpPost("Cancel/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromRoute] int id)
        {
            var me = CurrentUserId;
            var req = await _db.DoctorBlockRequests
                        .FirstOrDefaultAsync(r => r.Id == id && r.DoctorId == me);
            if (req == null) return NotFound();

            if (req.Status is BlockRequestStatus.Pending or BlockRequestStatus.Approved)
            {
                req.Status = BlockRequestStatus.Cancelled;
                req.DecisionByUserId = me;
                req.DecisionAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }

            return BadRequest("Only Pending or Approved requests can be cancelled.");
        }
    }
}
