// ==UserScript==
// @name         Antenati
// @description  Addon GeneaGrab pour Antenati
// @icon         https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab/Assets/Logo/Icon.png
// @version      1.1.0
// @grant        none
// @match        https://antenati.cultura.gov.it/**
// @updateURL    https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab.WebScripts/Antenati.user.js
// @downloadURL  https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab.WebScripts/Antenati.user.js
// ==/UserScript==

window.addEventListener("load", function () {
    if (document.getElementById("search_list")) search();
    else if (document.getElementById("mirador")) viewer();
}, false);

function search() {
    for (let item of document.querySelectorAll(".item-detail > .button.secondary")) {
        let openInGeneagrab = document.createElement("a");
        item.before(openInGeneagrab);
        openInGeneagrab.classList.add("button");
        openInGeneagrab.classList.add("primary");
        openInGeneagrab.innerText = "Open in GeneaGrab";
        openInGeneagrab.setAttribute("href", "geneagrab:registry?url=" + encodeURIComponent(item.getAttribute("href")));
    }
}

function viewer() {
    const bookmarkBtn = document.getElementsByClassName("item-share")[0];

    const openInGeneagrab = document.createElement("div");
    openInGeneagrab.innerHTML = '<img src="https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab/Assets/Logo/Icon.png" style="width: 1.33em; vertical-align: sub;"/>Open in GeneaGrab';
    openInGeneagrab.addEventListener("click", function (e) {
        e.preventDefault();
        window.location = "geneagrab:registry?url=" + encodeURIComponent(window.location);
    });

    bookmarkBtn.after(openInGeneagrab);
}
