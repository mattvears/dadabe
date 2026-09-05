(function () {
    var STORAGE_KEY = 'wa-color-scheme';

    function applyScheme(dark) {
        document.documentElement.classList.toggle('wa-dark', dark);
    }

    function getPreferredScheme() {
        var saved = localStorage.getItem(STORAGE_KEY);
        if (saved !== null) return saved === 'dark';
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    // Apply immediately so there's no flash of the wrong scheme on load.
    applyScheme(getPreferredScheme());

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (event) {
        if (localStorage.getItem(STORAGE_KEY) === null) {
            applyScheme(event.matches);
        }
    });

    document.addEventListener('click', function (event) {
        var button = event.target.closest && event.target.closest('#color-scheme-button');
        if (!button) return;
        var toDark = !document.documentElement.classList.contains('wa-dark');
        applyScheme(toDark);
        localStorage.setItem(STORAGE_KEY, toDark ? 'dark' : 'light');
    });
})();

// Marks a clicked chord row/link as the active one. Result cards stack, so the
// highlight is cleared only within the card that owns the clicked element.
window.selectChordRow = function (el) {
    var scope = el.closest('.result-card') || document;
    scope.querySelectorAll('.pred-row--active, .chord-link--active').forEach(function (other) {
        other.classList.remove('pred-row--active', 'chord-link--active');
    });
    el.classList.add(el.classList.contains('chord-link') ? 'chord-link--active' : 'pred-row--active');
};

// Pins a voicing-result row to the active song section (v0.5.2 §9). Reads
// the row's static data via attributes (set server-side) and the dynamic
// active song/section straight from the header selects.
window.pinVoicing = function (btn) {
    var songSel = document.getElementById('global-song-select');
    var sectionSel = document.getElementById('global-section-select');
    var slug = songSel && songSel.value;
    var sectionId = sectionSel && sectionSel.value;
    var status = btn.closest('.result-card') && btn.closest('.result-card').querySelector('.pin-status');

    if (!slug || !sectionId) {
        if (status) { status.textContent = 'Select an active song and section (top right) before pinning.'; }
        return;
    }

    var body = new URLSearchParams();
    body.set('slug', slug);
    body.set('sectionId', sectionId);
    body.set('symbol', btn.getAttribute('data-symbol') || '');
    body.set('bars', '1');
    body.set('comfortPct', btn.getAttribute('data-comfort') || '0');
    body.set('structure', btn.getAttribute('data-structure') || '');
    body.set('positions', btn.getAttribute('data-positions') || '[]');

    fetch('/api/songs/pin', { method: 'POST', body: body })
        .then(function (r) { return r.text(); })
        .then(function (html) { if (status) { status.innerHTML = html; } });
};

// Bridges the shell's global tuning select to a page's hidden form fields
// (#form-tuning / #form-tuning-name). Pages call window.bindTuningSelect()
// from their own inline script after render.
(function () {
    var STORAGE_KEY = 'dadabe-tuning';
    var sel, tuningField, nameField;

    function getLabel(val) {
        var opt = sel.querySelector('wa-option[value="' + val + '"]');
        return (opt && opt.getAttribute('data-name')) || val;
    }

    function write(val) {
        if (!val || val === 'undefined') return;
        if (tuningField) tuningField.value = val;
        if (nameField) nameField.value = getLabel(val);
        localStorage.setItem(STORAGE_KEY, val);
    }

    // wa-select emits the standard `change` event, not `wa-change`.
    function onChange() { write(sel.value); }

    // Options arrive via the select's own hx-get. Write the fields straight from
    // localStorage rather than round-tripping through sel.value, which may not be
    // ready if wa-select is still processing freshly-injected options.
    function restore() {
        var saved = localStorage.getItem(STORAGE_KEY);
        if (saved) { sel.value = saved; write(saved); }
        else { write(sel.value); }
    }

    function onAfterSwap() { setTimeout(restore, 0); }

    window.bindTuningSelect = function () {
        sel = document.getElementById('global-tuning-select');
        if (!sel) return;
        tuningField = document.getElementById('form-tuning');
        nameField = document.getElementById('form-tuning-name');
        sel.removeAttribute('disabled');

        // The select lives in the shell and outlives page swaps, so replace the
        // listeners each time instead of stacking one per navigation.
        sel.removeEventListener('change', onChange);
        sel.addEventListener('change', onChange);
        sel.removeEventListener('htmx:afterSwap', onAfterSwap);
        sel.addEventListener('htmx:afterSwap', onAfterSwap);

        restore();
    };

    // Exposed so the song binding (below) can force the tuning select to a
    // song's tuning without waiting on a real user interaction.
    window.setActiveTuning = function (val) {
        if (!sel || !val) return;
        sel.value = val;
        write(val);
    };
})();

// Active song / section (v0.5.2 §8, D37/D38). A song owns its tuning: while
// a song is active, the global tuning select is hidden entirely (nothing to
// choose — every pinned shape in the song is meaningless in another tuning)
// and replaced with a plain text display of the song's own tuning.
(function () {
    var SONG_KEY = 'dadabe-active-song';
    var SECTION_KEY = 'dadabe-active-section';
    var songSel, sectionSel, tuningSel, tuningDisplay;
    var activeSongTuning = null;

    function songField() { return document.getElementById('form-song'); }
    function sectionField() { return document.getElementById('form-section'); }

    function writeHiddenFields() {
        var sf = songField(), ef = sectionField();
        if (sf) { sf.value = (songSel && songSel.value) || ''; }
        if (ef) { ef.value = (sectionSel && sectionSel.value) || ''; }
    }

    function loadSections(slug, restoreId) {
        if (!sectionSel) { return; }
        if (!slug) {
            sectionSel.innerHTML = '';
            sectionSel.style.display = 'none';
            return;
        }
        fetch('/api/songs/' + encodeURIComponent(slug) + '/sections/options')
            .then(function (r) { return r.text(); })
            .then(function (html) {
                sectionSel.innerHTML = html;
                var hasSections = sectionSel.querySelectorAll('wa-option').length > 0;
                sectionSel.style.display = hasSections ? '' : 'none';
                if (hasSections) {
                    var toSelect = restoreId
                        && sectionSel.querySelector('wa-option[value="' + restoreId + '"]')
                        ? restoreId
                        : sectionSel.querySelector('wa-option').getAttribute('value');
                    sectionSel.value = toSelect;
                    localStorage.setItem(SECTION_KEY, toSelect || '');
                }
                writeHiddenFields();
            })
            .catch(function () { /* section list is a convenience; ignore fetch failure */ });
    }

    function onSongChange() {
        var slug = songSel.value;
        localStorage.setItem(SONG_KEY, slug || '');
        var opt = slug ? songSel.querySelector('wa-option[value="' + slug + '"]') : null;
        activeSongTuning = opt ? opt.getAttribute('data-tuning') : null;

        if (!slug) {
            if (tuningSel) { tuningSel.style.display = ''; }
            if (tuningDisplay) { tuningDisplay.style.display = 'none'; }
            loadSections(null);
            writeHiddenFields();
            return;
        }

        if (activeSongTuning && window.setActiveTuning) {
            window.setActiveTuning(activeSongTuning);
        }
        if (tuningSel) { tuningSel.style.display = 'none'; }
        if (tuningDisplay) {
            tuningDisplay.textContent = activeSongTuning || '(tuning not set)';
            tuningDisplay.style.display = '';
        }
        loadSections(slug, localStorage.getItem(SECTION_KEY));
    }

    function onSectionChange() {
        localStorage.setItem(SECTION_KEY, sectionSel.value || '');
        writeHiddenFields();
    }

    function restore() {
        var savedSong = localStorage.getItem(SONG_KEY);
        if (savedSong && songSel.querySelector('wa-option[value="' + savedSong + '"]')) {
            songSel.value = savedSong;
        }
        onSongChange();
    }

    window.bindSongSelect = function () {
        songSel = document.getElementById('global-song-select');
        sectionSel = document.getElementById('global-section-select');
        tuningSel = document.getElementById('global-tuning-select');
        tuningDisplay = document.getElementById('song-tuning-display');
        if (!songSel) { return; }

        songSel.removeEventListener('change', onSongChange);
        songSel.addEventListener('change', onSongChange);
        songSel.removeEventListener('htmx:afterSwap', restore);
        songSel.addEventListener('htmx:afterSwap', restore);

        if (sectionSel) {
            sectionSel.removeEventListener('change', onSectionChange);
            sectionSel.addEventListener('change', onSectionChange);
        }

        restore();
    };

    document.addEventListener('DOMContentLoaded', function () { window.bindSongSelect(); });
})();
