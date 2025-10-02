// This file is injected directly into the page context (not the isolated content script world).
// It can read global JS variables the page sets (if any) and expose them on window.__CROSSCLIMB_STATE__


(function () {
    try {
        // Try to find probable global variables or React props. These are guesses — you must adapt.
        const state = {};


        // Example heuristics (these will likely need adjustment):
        if (window.CrossclimbGame) state._foundCrossclimbGlobal = true;
        if (window.__CROSSCLIMB_STATE__) state._already = true;


        // If the game exposes a global object, copy some fields
        if (window.CrossclimbGame && typeof window.CrossclimbGame.getState === 'function') {
            try { state.game = window.CrossclimbGame.getState(); } catch (e) { }
        }


        // Another trick: find React root and try to extract props from mounted component (complex and fragile)
        // We'll set the collected state onto the page window so content.js can read it.
        window.__CROSSCLIMB_STATE__ = state;
    } catch (e) {
        console.error('inpage injection error', e);
    }
})();