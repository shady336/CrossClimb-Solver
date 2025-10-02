# CrossClimb Helper for crossclimb-game.com

## Install

1. Open Microsoft Edge.
2. Go to `edge://extensions`, enable *Developer mode*.
3. Click *Load unpacked* and select the extension folder containing the above files.
4. Navigate to `https://crossclimb-game.com/?puzzleId=1`.

## Usage

- Click the extension icon, then click **Scrape Game State** to get JSON output of things like puzzleId, score, moves, timer, grid HTML, canvas image data.
- Use **Auto-fill Input** to attempt to fill some input/answer field with text (you’ll need to adjust the `'text'` in popup.js or modify payload via popup UI).

## Adjustments you’ll likely need to make

- Confirm selectors in `content.js`: `.score`, `.moves`, `.timer`, `.grid`, `.input-field`, etc. Use browser devtools (inspect elements) to get accurate class/ID names.
- If the game's input is not a native input/textarea but custom (e.g. React component or canvas), you may need to simulate keyboard events or via inpage script.
- Canvas scraping: `canvas.toDataURL()` will work only if the canvas is same-origin and not tainted by cross-origin images. If it fails, you’ll get an exception.

## Possible enhancements

- Add OCR on canvas contents if needed (with Tesseract.js or similar).
- Provide a small UI in the popup to enter the fill-text instead of hardcoding.
- Store state/results in `chrome.storage` to keep data across navigations.
