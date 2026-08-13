// Shared behaviour for the Login / Register pages.
(function () {
    window.togglePassword = function (inputId) {
        var input = document.getElementById(inputId);
        if (!input) return;
        input.type = input.type === "password" ? "text" : "password";
    };
})();
