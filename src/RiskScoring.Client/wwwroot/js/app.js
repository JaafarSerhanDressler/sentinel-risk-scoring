// Sentinel PWA helpers: install prompt capture + lightweight storage.
window.sentinel = {
    deferredPrompt: null,
    _ref: null,

    init: function (dotnetRef) {
        window.sentinel._ref = dotnetRef;
        window.addEventListener('beforeinstallprompt', function (e) {
            e.preventDefault();
            window.sentinel.deferredPrompt = e;
            if (dotnetRef) { try { dotnetRef.invokeMethodAsync('OnInstallAvailable'); } catch (_) { } }
        });
        window.addEventListener('appinstalled', function () {
            window.sentinel.deferredPrompt = null;
            if (dotnetRef) { try { dotnetRef.invokeMethodAsync('OnInstalled'); } catch (_) { } }
        });
    },

    canInstall: function () { return !!window.sentinel.deferredPrompt; },

    promptInstall: async function () {
        const p = window.sentinel.deferredPrompt;
        if (!p) return 'unavailable';
        p.prompt();
        const choice = await p.userChoice;
        window.sentinel.deferredPrompt = null;
        return choice.outcome;
    },

    store: {
        get: function (k) { try { return localStorage.getItem(k); } catch (_) { return null; } },
        set: function (k, v) { try { localStorage.setItem(k, v); } catch (_) { } },
        remove: function (k) { try { localStorage.removeItem(k); } catch (_) { } }
    }
};
