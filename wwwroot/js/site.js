// Client-side rule: date must not be in the past (optionally exempt when another field has a given value).
// Paired with the server-side [NotPastDate] attribute - the server remains the authority.
(function ($) {
    if (!$ || !$.validator) return;
    $.validator.addMethod("notpast", function (value, element, params) {
        if (!value) return true;
        if (params && params.dependson) {
            var dep = $("[name='" + params.dependson + "']").val();
            if (dep && (params.exempt || "").split(",").indexOf(dep) >= 0) return true;
        }
        var today = new Date(); today.setHours(0, 0, 0, 0);
        var d = new Date(value + "T00:00:00");
        return !isNaN(d.getTime()) && d >= today;
    });
    $.validator.unobtrusive.adapters.add("notpast", ["dependson", "exempt"], function (options) {
        options.rules["notpast"] = { dependson: options.params.dependson, exempt: options.params.exempt };
        options.messages["notpast"] = options.message;
    });
    $(document).on("change", "select[name='Stage'], select[name='Status']", function () {
        $("input[data-val-notpast]").each(function () { $(this).valid(); });
    });
})(window.jQuery);
