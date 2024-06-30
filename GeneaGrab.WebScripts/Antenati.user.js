// ==UserScript==
// @name         Antenati
// @description  Addon GeneaGrab pour Antenati
// @icon         https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab/Assets/Logo/Icon.png
// @version      1.0.0
// @grant        none
// @match        https://antenati.cultura.gov.it/ark:/**
// @updateURL    https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab.WebScripts/Antenati.user.js
// @downloadURL  https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab.WebScripts/Antenati.user.js
// ==/UserScript==

const bookmarkBtn = document.getElementsByClassName("item-share")[0];

const openInGeneagrab = document.createElement("div");
openInGeneagrab.style.display = "block";
openInGeneagrab.innerHTML = '<img src="https://github.com/06Games/GeneaGrab/raw/v2/GeneaGrab/Assets/Logo/Icon.png" style="width: 1.33em; vertical-align: sub;"/>Open in GeneaGrab';
openInGeneagrab.addEventListener("click", function (e) {
    e.preventDefault();
    window.location = "geneagrab:registry?url=" + encodeURIComponent(window.location);
});

bookmarkBtn.after(openInGeneagrab);
