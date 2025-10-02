class CrossClimbHelper {
    constructor() {
        this.wordLength = this.getWordLength();
        this.ladderWords = [];
    }

    // Scrape word length
    getWordLength() {
        const firstRow = document.querySelector(".grid.grid-cols-5");
        if (!firstRow) return 0;
        const letters = Array.from(firstRow.querySelectorAll("input"))
            .map((i) => i.value || "_")
            .join("");
        return letters.length;
    }

    // Scrape ladder clues by clicking each row
    async getLadderClues() {
        // Select each row by its container
        const parent = document.querySelector(
            "#root > div > div > div.flex-1.flex.flex-col.items-center.justify-start.px-2.sm\\:px-4.py-2.sm\\:py-8 > div > div.w-full.mx-auto.px-7.sm\\:px-8.sm\\:transform.sm\\:scale-100 > div"
        );
        const rowContainers = parent ? Array.from(parent.children) : [];
        const clues = [];
        // Iterate on the ladder inputs (Excluding the ends as we don't have them at the moment)
        for (let i = 1; i < rowContainers.length - 1; i++) {
            const firstInput = rowContainers[i].querySelector("input");

            firstInput.click();

            await new Promise((resolve) => setTimeout(resolve, 150));

            const clueEl = document.querySelector(".bg-gray-50 p");
            const clueText = clueEl ? clueEl.innerText.trim() : null;

            clues.push(clueText);
        }
        return clues;
    }

    // Scrape ends clues by clicking the first and last row
    async getEndsClues() {
        // Select each row by its container
        const parent = document.querySelector(
            "#root > div > div > div.flex-1.flex.flex-col.items-center.justify-start.px-2.sm\\:px-4.py-2.sm\\:py-8 > div > div.w-full.mx-auto.px-7.sm\\:px-8.sm\\:transform.sm\\:scale-100 > div"
        );
        const rowContainers = parent ? Array.from(parent.children) : [];
        const clues = {};
        // Click the first row
        const firstInput = rowContainers[0].querySelector("input");
        firstInput.click();
        await new Promise((resolve) => setTimeout(resolve, 150));
        const firstClueEl = document.querySelector(".bg-gray-50 p");
        const firstClueText = firstClueEl ? firstClueEl.innerText.trim() : null;
        clues.topClue = firstClueText;
        // Click the last row
        const lastInput = rowContainers[rowContainers.length - 1].querySelector("input");
        lastInput.click();
        await new Promise((resolve) => setTimeout(resolve, 150));
        const lastClueEl = document.querySelector(".bg-gray-50 p");
        const lastClueText = lastClueEl ? lastClueEl.innerText.trim() : null;
        clues.bottomClue = lastClueText;
        return clues;
    }

    // Autofill a row with a word (row index: 0-based)
    fillRow(index, word) {
        const row = document.querySelectorAll(".grid.grid-cols-5")[index];
        console.log("Filling row", index, "with word", word, row);
        if (!row) return;
        const inputs = row.querySelectorAll("input");
        word.split("").forEach((char, i) => {
            if (inputs[i]) {
                inputs[i].value = char.toUpperCase();
                inputs[i].dispatchEvent(new Event("input", { bubbles: true }));
            }
        });
    }

    async solveLadder() {
        // Make POST request to the specified endpoint
        let returnedResponse;
        await fetch("https://ali-func-inprocess-4870.azurewebsites.net/api/solve/ladder", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                wordLength: this.wordLength,
                clues: await this.getLadderClues()
            })
        })
            .then(response => response.json())
            .then(data => {
                returnedResponse = { apiResponse: data };
                this.ladderWords = data["ladder"];
                for (let i = 0; i < this.ladderWords.length; i++) {
                    this.fillRow(i + 1, this.ladderWords[i]); // +1 to skip the first row
                }
            })
            .catch(error => {
                returnedResponse = { apiResponse: { error: error.toString() } };
            });
        // Verify if the ladder needs to be reversed
        this.verifyLadderOrder();
        return returnedResponse;
    }

    verifyLadderOrder() {
        console.log("Verifying ladder order...");
        const clueEl = document.querySelector(".bg-gray-50 p");
        const clueText = clueEl ? clueEl.innerText.trim() : null;
        if (clueText == "Reorder the rows to form a word ladder, where each word differs by one letter.") {
            this.ladderWords.reverse();
            for (let i = 0; i < this.ladderWords.length; i++) {
                this.fillRow(i + 1, this.ladderWords[i]); // +1 to skip the first row
            }
        }
    }

    async solveOneEnd(clue, neighborWord, isTop) {
        // Make POST request to the specified endpoint
        let endsResponse;
        await fetch("https://ali-func-inprocess-4870.azurewebsites.net/api/solve/ends", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                clue: clue,
                wordLength: this.wordLength,
                neighborWord: neighborWord
            })
        })
            .then(response => response.json())
            .then(data => {
                endsResponse = { data };
                const endWord = data["answer"];
                if (isTop) {
                    this.fillRow(0, endWord);
                } else {
                    this.fillRow(this.ladderWords.length + 1, endWord);
                }
            })
            .catch(error => {
                endsResponse = { error: error.toString() };
            });
        return endsResponse;
    }

    async solveEnds() {
        const endsClues = await this.getEndsClues();
        let firstEndResponse = await this.solveOneEnd(endsClues.topClue, this.ladderWords[0], true);
        let secondEndResponse = await this.solveOneEnd(endsClues.bottomClue, this.ladderWords[this.ladderWords.length - 1], false);
        return { firstEndResponse, secondEndResponse };
    }
}

const crossClimbHelper = new CrossClimbHelper();

// Listener for extension messages
chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg.action === "solveLadder") {
        crossClimbHelper.solveLadder().then((response) => {
            sendResponse(response);
        });
        return true;
    } else if (msg.action === "solveEnds") {
        crossClimbHelper.solveEnds().then((response) => {
            sendResponse(response);
        });
        return true;
    }
});
