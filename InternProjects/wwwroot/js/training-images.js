// Качване на снимки в редактора на началното обучение.
// Работи по AJAX, за да не се губи неспазеният текст в полето при качване.
(function () {
    "use strict";

    var panel = document.querySelector(".training-images");
    if (!panel) return;

    var textarea = document.querySelector('textarea[name="Content"]');
    var input = panel.querySelector(".js-image-input");
    var list = panel.querySelector(".js-image-list");
    var empty = panel.querySelector(".js-image-empty");
    var errorBox = panel.querySelector(".js-image-error");
    var progress = panel.querySelector(".js-image-progress");

    function token() {
        var field = document.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : "";
    }

    function showError(message) {
        errorBox.textContent = message;
        errorBox.classList.remove("d-none");
    }

    function clearError() {
        errorBox.textContent = "";
        errorBox.classList.add("d-none");
    }

    function refreshEmpty() {
        var hasImages = list.querySelector(".js-image-item") !== null;
        empty.classList.toggle("d-none", hasImages);
    }

    // Вмъква маркера на мястото на курсора, винаги на самостоятелен ред.
    function insertMarker(marker) {
        if (!textarea) return;

        var start = textarea.selectionStart;
        var end = textarea.selectionEnd;

        if (typeof start !== "number") {
            textarea.value += "\n\n" + marker + "\n";
            return;
        }

        var before = textarea.value.slice(0, start);
        var after = textarea.value.slice(end);

        var prefix = before.length === 0 || before.endsWith("\n") ? "" : "\n";
        var suffix = after.length === 0 || after.startsWith("\n") ? "" : "\n";
        var insertion = prefix + marker + suffix;

        textarea.value = before + insertion + after;

        var caret = start + insertion.length;
        textarea.focus();
        textarea.setSelectionRange(caret, caret);
    }

    function buildItem(data) {
        var col = document.createElement("div");
        col.className = "col-6 js-image-item";
        col.setAttribute("data-image-id", data.id);

        var box = document.createElement("div");
        box.className = "border rounded p-2 h-100";

        var img = document.createElement("img");
        img.src = data.url;
        img.alt = data.originalName;
        img.className = "img-fluid rounded mb-2";

        var row = document.createElement("div");
        row.className = "d-flex align-items-center gap-1 mb-1";

        var code = document.createElement("code");
        code.className = "small flex-grow-1";
        code.textContent = data.marker;

        var insert = document.createElement("button");
        insert.type = "button";
        insert.className = "btn btn-sm btn-outline-primary js-insert";
        insert.textContent = "Вмъкни";

        var remove = document.createElement("button");
        remove.type = "button";
        remove.className = "btn btn-sm btn-outline-danger js-delete";
        remove.textContent = "✕";

        var caption = document.createElement("input");
        caption.type = "text";
        caption.className = "form-control form-control-sm js-caption";
        caption.placeholder = "Пояснение под снимката";

        row.appendChild(code);
        row.appendChild(insert);
        row.appendChild(remove);
        box.appendChild(img);
        box.appendChild(row);
        box.appendChild(caption);
        col.appendChild(box);

        return col;
    }

    function upload(file) {
        var data = new FormData();
        data.append("ownerType", panel.getAttribute("data-owner-type"));
        data.append("ownerId", panel.getAttribute("data-owner-id"));
        data.append("file", file);
        data.append("__RequestVerificationToken", token());

        return fetch(panel.getAttribute("data-upload-url"), {
            method: "POST",
            body: data
        }).then(function (response) {
            if (!response.ok) throw new Error("Качването не успя (" + response.status + ").");
            return response.json();
        }).then(function (result) {
            if (!result.ok) throw new Error(result.error);

            list.appendChild(buildItem(result));
            refreshEmpty();
            insertMarker(result.marker);
        });
    }

    input.addEventListener("change", function () {
        var files = Array.prototype.slice.call(input.files || []);
        if (files.length === 0) return;

        clearError();
        progress.classList.remove("d-none");

        files.reduce(function (chain, file) {
            return chain.then(function () { return upload(file); });
        }, Promise.resolve())
            .catch(function (err) { showError(err.message); })
            .then(function () {
                progress.classList.add("d-none");
                input.value = "";
            });
    });

    panel.addEventListener("click", function (event) {
        var item = event.target.closest(".js-image-item");
        if (!item) return;

        var id = item.getAttribute("data-image-id");

        if (event.target.closest(".js-insert")) {
            insertMarker("[img:" + id + "]");
            return;
        }

        if (event.target.closest(".js-delete")) {
            if (!confirm("Снимката ще бъде изтрита. Да продължа?")) return;

            clearError();
            var data = new FormData();
            data.append("id", id);
            data.append("__RequestVerificationToken", token());

            fetch(panel.getAttribute("data-delete-url"), { method: "POST", body: data })
                .then(function (r) { return r.json(); })
                .then(function (result) {
                    if (!result.ok) throw new Error(result.error);
                    item.remove();
                    refreshEmpty();
                })
                .catch(function (err) { showError(err.message); });
        }
    });

    panel.addEventListener("change", function (event) {
        if (!event.target.classList.contains("js-caption")) return;

        var item = event.target.closest(".js-image-item");
        if (!item) return;

        clearError();
        var data = new FormData();
        data.append("id", item.getAttribute("data-image-id"));
        data.append("caption", event.target.value);
        data.append("__RequestVerificationToken", token());

        fetch(panel.getAttribute("data-caption-url"), { method: "POST", body: data })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                if (!result.ok) throw new Error(result.error);
            })
            .catch(function (err) { showError(err.message); });
    });
})();
