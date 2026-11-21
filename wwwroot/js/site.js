// CampusCue — site.js
// Shared client-side interactions

(function () {
    'use strict';

    // ── Sidebar toggle (mobile) ──────────────────────────────────────────
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebarOverlay');
    const toggleBtn = document.getElementById('sidebarToggle');

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('open');
            overlay && overlay.classList.toggle('open');
        });
    }

    if (overlay) {
        overlay.addEventListener('click', function () {
            sidebar && sidebar.classList.remove('open');
            overlay.classList.remove('open');
        });
    }

    // ── Auto-dismiss alerts after 5 s ───────────────────────────────────
    document.querySelectorAll('.alert[data-auto-dismiss]').forEach(function (el) {
        setTimeout(function () {
            const bsAlert = window.bootstrap && bootstrap.Alert.getOrCreateInstance(el);
            bsAlert && bsAlert.close();
        }, 5000);
    });

    // ── Show/hide password toggle ────────────────────────────────────────
    document.querySelectorAll('[data-toggle-password]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const target = document.querySelector(btn.dataset.togglePassword);
            if (!target) return;
            const isPassword = target.type === 'password';
            target.type = isPassword ? 'text' : 'password';
            btn.textContent = isPassword ? '🙈' : '👁';
        });
    });

    // ── Confirm dialog for destructive actions ───────────────────────────
    document.querySelectorAll('[data-confirm]:not(form)').forEach(function (el) {
        el.addEventListener('click', function (e) {
            const msg = el.dataset.confirm || 'Are you sure?';
            if (!window.confirm(msg)) {
                e.preventDefault();
                e.stopPropagation();
            }
        });
    });

    // ── Confirm for form submit (delete forms) ───────────────────────────
    document.querySelectorAll('form[data-confirm]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            const msg = form.dataset.confirm || 'Are you sure?';
            if (!window.confirm(msg)) {
                e.preventDefault();
            }
        });
    });

    // ── Date/time validation for event forms ─────────────────────────────
    const startTimeInput = document.getElementById('StartTime');
    const endTimeInput = document.getElementById('EndTime');
    const eventDateInput = document.getElementById('EventDate');
    const deadlineInput = document.getElementById('RegistrationDeadline');

    function validateEventTimes() {
        if (endTimeInput && startTimeInput && endTimeInput.value && startTimeInput.value && endTimeInput.value <= startTimeInput.value) {
            endTimeInput.setCustomValidity('End time must be after start time.');
        } else if (endTimeInput) {
            endTimeInput.setCustomValidity('');
        }

        if (eventDateInput && deadlineInput && eventDateInput.value && deadlineInput.value) {
            const eventStart = new Date(eventDateInput.value + 'T' + (startTimeInput?.value || '00:00'));
            const deadline = new Date(deadlineInput.value);
            deadlineInput.setCustomValidity(deadline >= eventStart
                ? 'Registration deadline must be before the event start.'
                : '');
        }
    }

    startTimeInput && startTimeInput.addEventListener('change', validateEventTimes);
    endTimeInput && endTimeInput.addEventListener('change', validateEventTimes);
    eventDateInput && eventDateInput.addEventListener('change', validateEventTimes);
    deadlineInput && deadlineInput.addEventListener('change', validateEventTimes);

    // ── Capacity bar live update ─────────────────────────────────────────
    document.querySelectorAll('.capacity-bar-fill[data-pct]').forEach(function (bar) {
        const pct = parseFloat(bar.dataset.pct) || 0;
        bar.style.width = pct + '%';
        if (pct >= 100) bar.classList.add('danger');
        else if (pct >= 80) bar.classList.add('warning');
    });

    // ── Attendance select-all helpers ────────────────────────────────────
    const markAllPresent = document.getElementById('markAllPresent');
    const markAllAbsent = document.getElementById('markAllAbsent');

    if (markAllPresent) {
        markAllPresent.addEventListener('click', function () {
            document.querySelectorAll('select.attendance-select').forEach(function (sel) {
                sel.value = 'Present';
            });
        });
    }

    if (markAllAbsent) {
        markAllAbsent.addEventListener('click', function () {
            document.querySelectorAll('select.attendance-select').forEach(function (sel) {
                sel.value = 'Absent';
            });
        });
    }

    // ── jQuery AJAX: event search, registration, and cancellation ────────
    if (window.jQuery) {
        const $ = window.jQuery;
        const $searchForm = $('[data-ajax-event-search]');
        let searchTimer;
        let activeSearch;

        function loadEventResults(page, updateAddress) {
            if (!$searchForm.length) return;
            if (activeSearch) activeSearch.abort();

            const query = $searchForm.serializeArray().filter(function (item) { return item.name !== 'page'; });
            if (page && page !== 1) query.push({ name: 'page', value: page });
            const queryString = $.param(query);
            const url = $searchForm.attr('action') + (queryString ? '?' + queryString : '');
            const $status = $('[data-ajax-status]');
            const $results = $('#event-results');

            $status.text('Loading events…');
            $searchForm.find(':submit').prop('disabled', true);
            activeSearch = $.get($searchForm.attr('action'), queryString)
                .done(function (html) {
                    $results.html(html);
                    $status.text('Event results updated.');
                    if (updateAddress) window.history.replaceState({}, '', url);
                })
                .fail(function (xhr, status) {
                    if (status !== 'abort') $status.text('Could not load events. Please try again.');
                })
                .always(function () { $searchForm.find(':submit').prop('disabled', false); });
        }

        $searchForm.on('submit', function (event) {
            event.preventDefault();
            loadEventResults(1, true);
        });
        $searchForm.find('select, input[type="checkbox"]').on('change', function () { loadEventResults(1, true); });
        $searchForm.find('input[name="search"]').on('input', function () {
            window.clearTimeout(searchTimer);
            searchTimer = window.setTimeout(function () { loadEventResults(1, true); }, 350);
        });
        $searchForm.on('click', '[data-ajax-clear]', function (event) {
            event.preventDefault();
            $searchForm.find('input[name="search"]').val('');
            $searchForm.find('select[name="categoryId"]').val('');
            $searchForm.find('input[name="upcomingOnly"]').prop('checked', false);
            loadEventResults(1, true);
        });
        $('#event-results').on('click', '[data-ajax-page]', function (event) {
            event.preventDefault();
            loadEventResults(Number($(this).data('ajax-page')), true);
            window.scrollTo({ top: $('#event-results').offset().top - 24, behavior: 'smooth' });
        });

        function showAjaxMessage(message, isError) {
            $('#registration-feedback').html($('<div>', {
                'class': 'alert ' + (isError ? 'alert-danger' : 'alert-success'),
                role: 'status',
                text: message || 'The request could not be completed.'
            }));
        }

        // $.post sends the form data and anti-forgery token for event registration.
        $(document).on('submit', 'form[data-ajax-action="register"]', function (event) {
            event.preventDefault();
            const form = this;
            const $button = $(form).find(':submit').prop('disabled', true);
            $.post(form.action, $(form).serialize(), null, 'json')
                .done(function (response) {
                    showAjaxMessage(response.message, false);
                    $(form).remove();
                })
                .fail(function (xhr) {
                    const response = xhr.responseJSON;
                    showAjaxMessage(response && response.message ? response.message : 'Registration failed. Please refresh and try again.', true);
                    $button.prop('disabled', false);
                });
        });

        // $.ajax submits cancellation with an explicit method and JSON response.
        $(document).on('submit', 'form[data-ajax-action="cancel"]', function (event) {
            event.preventDefault();
            const form = this;
            const $form = $(form);
            const $button = $form.find(':submit').prop('disabled', true);
            $.ajax({ url: form.action, method: 'POST', data: $form.serialize(), dataType: 'json' })
                .done(function (response) {
                    showAjaxMessage(response.message, false);
                    $form.closest('tr').fadeOut(180, function () { $(this).remove(); });
                })
                .fail(function (xhr) {
                    const response = xhr.responseJSON;
                    showAjaxMessage(response && response.message ? response.message : 'Cancellation failed. Please refresh and try again.', true);
                    $button.prop('disabled', false);
                });
        });

        // Project-wide AJAX navigation for the remaining MVC GET filters,
        // pagers, and POST forms. The server still renders the same Razor page,
        // preserving existing validation, authorization, and redirect flows.
        function displayAjaxPage(html, responseUrl) {
            if (!html || !/<html[\s>]/i.test(html)) return false;
            if (responseUrl && responseUrl !== window.location.href) {
                window.history.pushState({}, '', responseUrl);
            }
            document.open();
            document.write(html);
            document.close();
            return true;
        }

        function showRequestError(form, message) {
            let $alert = $(form).find('[data-ajax-error]');
            if (!$alert.length) {
                $alert = $('<div data-ajax-error class="alert alert-danger mt-3" role="alert"></div>');
                $(form).append($alert);
            }
            $alert.text(message);
        }

        // Existing event search has a dedicated partial refresh; other GET
        // filter forms retrieve their Razor response through $.get.
        $(document).on('submit', 'form[method="get"]:not([data-ajax-event-search])', function (event) {
            event.preventDefault();
            const form = this;
            const baseUrl = new URL(form.action, window.location.href);
            baseUrl.search = '';
            const queryString = $(form).serialize();
            const url = baseUrl.pathname + (queryString ? '?' + queryString : '');
            const $button = $(form).find(':submit').prop('disabled', true);
            $.get(url)
                .done(function (html, status, xhr) {
                    if (!displayAjaxPage(html, xhr.responseURL || url)) showRequestError(form, 'The page response was not valid HTML.');
                })
                .fail(function () { showRequestError(form, 'Could not load this page. Please try again.'); })
                .always(function () { $button.prop('disabled', false); });
        });

        $(document).on('click', 'a.page-link[href]:not([data-ajax-page])', function (event) {
            if (this.closest('.disabled')) return;
            event.preventDefault();
            const link = this;
            $.get(link.href)
                .done(function (html, status, xhr) {
                    if (!displayAjaxPage(html, xhr.responseURL || link.href)) window.location.assign(link.href);
                })
                .fail(function () { window.location.assign(link.href); });
        });

        // All regular MVC POST forms use AJAX, including authentication,
        // profile, admin, organizer, category, attendance, and lifecycle forms.
        $(document).on('submit', 'form[method="post"]:not([data-ajax-action])', function (event) {
            const form = this;
            if (form.enctype && form.enctype.toLowerCase().indexOf('multipart/form-data') >= 0) return;
            event.preventDefault();

            const $form = $(form);
            const $buttons = $form.find(':submit').prop('disabled', true);
            const request = $.ajax({
                url: form.action,
                method: 'POST',
                data: $form.serialize(),
                dataType: 'html'
            });

            request.done(function (html, status, xhr) {
                if (!displayAjaxPage(html, xhr.responseURL || form.action)) {
                    showRequestError(form, 'The server returned an unexpected response.');
                    $buttons.prop('disabled', false);
                }
            });
            request.fail(function (xhr) {
                const contentType = xhr.getResponseHeader('Content-Type') || '';
                if (contentType.indexOf('text/html') >= 0 && displayAjaxPage(xhr.responseText, xhr.responseURL || form.action)) return;
                showRequestError(form, 'The request failed. Please check your connection and try again.');
                $buttons.prop('disabled', false);
            });
        });
    }

    // ── Search auto-submit on filter change ──────────────────────────────
    document.querySelectorAll('[data-auto-submit]').forEach(function (el) {
        el.addEventListener('change', function () {
            const form = el.closest('form');
            form && form.submit();
        });
    });

})();
