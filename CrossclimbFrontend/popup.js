const spinner = document.getElementById('spinner');
const solveLadderBtn = document.getElementById('solveLadderBtn');
const solveEndsBtn = document.getElementById('solveEndsBtn');

solveLadderBtn.addEventListener('click', function() {
    spinner.style.display = 'block';
    solveLadderBtn.disabled = true;
    solveEndsBtn.disabled = true;
    chrome.tabs.query({ active: true, currentWindow: true }, function(tabs) {
        chrome.tabs.sendMessage(
            tabs[0].id,
            { action: 'solveLadder' },
            function(response) {
                spinner.style.display = 'none';
                solveLadderBtn.disabled = false;
                solveEndsBtn.disabled = false;
                if (response) {
                    document.getElementById('solveLadderResponse').textContent = JSON.stringify(response.apiResponse, null, 2);
                } else {
                    document.getElementById('solveLadderResponse').textContent = 'No response or error.';
                }
            }
        );
    });
});

solveEndsBtn.addEventListener('click', function() {
    spinner.style.display = 'block';
    solveLadderBtn.disabled = true;
    solveEndsBtn.disabled = true;
    chrome.tabs.query({ active: true, currentWindow: true }, function(tabs) {
        chrome.tabs.sendMessage(
            tabs[0].id,
            { action: 'solveEnds' },
            function(response) {
                spinner.style.display = 'none';
                solveLadderBtn.disabled = false;
                solveEndsBtn.disabled = false;
                if (response) {
                    document.getElementById('solveEndsResponse').textContent = JSON.stringify(response, null, 2);
                } else {
                    document.getElementById('solveEndsResponse').textContent = 'No response or error.';
                }
            }
        );
    });
});