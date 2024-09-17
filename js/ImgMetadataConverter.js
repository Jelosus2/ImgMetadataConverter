const imgMetadataConverterConfirmer = getRequiredElementById("imgmetadataconverter_settings_confirmer");
const imgMetadataConverterCount = getRequiredElementById("imgmetadataconverter_settings_edit_count");
const imgMetadataConverterHashTable = getRequiredElementById("imgmetadataconverter_hashtable_tbody");
const imgMetadataConverterHashTableStatusMsg = getRequiredElementById("imgmetadataconverter_hashtable_statusmsg");
const imgMetadataConverterHashTableCivitaiResultsContainer = getRequiredElementById("imgmetadataconverter_hashtable_civitai_results_container");
const imgMetadataConverterHashTableCivitaiResultMsg = getRequiredElementById("imgmetadataconverter_hashtable_civitai_result_msg");

let imgMetadataConverterSettingsData = {
    known: {},
    altered: {}
};

let isImgMetadataConverterBussy = false;

let elementIds = ["active", "cache", "outputDirectory", "skipDuplicates", "appendOutPathBuild"];
function buildImgMetadataConverterSettingsMenu(data) {
    for (let elementId of elementIds) {
        let element = getRequiredElementById(`imgmetadataconverter_settings_${elementId.toLowerCase()}`);
        
        if (element.type == "checkbox") {
            if (data[elementId] == undefined) {
                imgMetadataConverterSettingsData.altered[elementId] = element.checked;
            } else {
                element.checked = data[elementId];
            }
        } else {
            if (data[elementId] == undefined) {
                imgMetadataConverterSettingsData.altered[elementId] = element.value;
            } else {
                element.value = data[elementId];
            }
        }
        imgMetadataConverterSettingsData.known[elementId] = data[elementId];

        if (Object.keys(imgMetadataConverterSettingsData.altered).length > 0) {
            saveImgMetadataConverterSettings();
        }

        element.addEventListener("input", () => {
            let value = "";

            if (element.type == 'checkbox') {
                value = element.checked;
            } else {
                value = element.value;
            }

            if (value == imgMetadataConverterSettingsData.known[elementId]) {
                delete imgMetadataConverterSettingsData.altered[elementId];
            } else {
                imgMetadataConverterSettingsData.altered[elementId] = value;
            }

            let count = Object.keys(imgMetadataConverterSettingsData.altered).length;
            imgMetadataConverterCount.innerText = count;
            imgMetadataConverterConfirmer.style.display = count == 0 ? "none" : "block";
        });
    }
}

function buildImgMetadataConverterHashTable(data, refreshStatus) {
    if (refreshStatus) {
        imgMetadataConverterHashTableStatusMsg.innerHTML = "";
        imgMetadataConverterHashTableCivitaiResultMsg.innerHTML = "";
        imgMetadataConverterHashTableCivitaiResultsContainer.innerHTML = "";
    }

    toggleImgMetadataConverterHashTableButtons();

    imgMetadataConverterHashTable.querySelectorAll("tr").forEach((trChild) => {
        if (trChild.querySelector("td")) {
            trChild.remove();
        }
    });

    let tr = document.createElement("tr");

    if (data.modelsWithHashes.length == 0) {
        let td = document.createElement("td");
        td.innerText = "No resources found";
        td.colSpan = "5";
        td.style.textAlign = "center";

        tr.appendChild(td);

        imgMetadataConverterHashTable.appendChild(tr);
    } else {
        for (let model of data.modelsWithHashes) {
            let resourceTd = document.createElement("td");
            let hashTd = document.createElement("td");
            let deleteTd = document.createElement("td");
            let calculateTd = document.createElement("td");
            let searchTd = document.createElement("td");

            resourceTd.innerText = model.modelName.split('/');

            hashTd.innerText = model.modelHash || "Not Calculated";
            hashTd.style.textAlign = "center";
            
            deleteTd.style.textAlign = "center";
            calculateTd.style.textAlign = "center";
            searchTd.style.textAlign = "center";

            let deleteBtn = document.createElement("button");
            deleteBtn.type = "button";
            deleteBtn.disabled = model.modelHash == "";
            deleteBtn.style.opacity = deleteBtn.disabled ? "0.5" : "1";
            deleteBtn.style.border = "none";
            deleteBtn.style.backgroundColor = "inherit";
            deleteBtn.setAttribute("onclick", `deleteImgMetadataConverterHashes('${model.modelName.replace(/'/g, "\\'")}')`);

            let calculateBtn = document.createElement("button");
            calculateBtn.type = "button";
            calculateBtn.disabled = model.modelHash != "";
            calculateBtn.style.opacity = calculateBtn.disabled ? "0.5" : "1";
            calculateBtn.style.border = "none";
            calculateBtn.style.backgroundColor = "inherit";
            calculateBtn.setAttribute("onclick", `calculateImgMetadataConverterHash('${model.modelName.replace(/'/g, "\\'")}')`);

            let searchBtn = document.createElement("button");
            searchBtn.type = "button";
            searchBtn.disabled = model.modelHash == "";
            searchBtn.style.opacity = searchBtn.disabled ? "0.5" : "1";
            searchBtn.style.border = "none";
            searchBtn.style.backgroundColor = "inherit";
            if (!searchBtn.disabled) {
                searchBtn.setAttribute("onclick", `imgMetadataConverterCivitaiSearch('${model.modelHash}')`);
            }

            let deleteIcon = document.createElement("img");
            deleteIcon.src = "/ExtensionFile/ImgMetadataConverter/assets/trash-can.svg";
            deleteIcon.style.width = "24px";

            let searchIcon = document.createElement("img");
            searchIcon.src = "/ExtensionFile/ImgMetadataConverter/assets/globe.svg";
            searchIcon.style.width = "24px";

            let calculateIcon = document.createElement("img");
            calculateIcon.src = "/ExtensionFile/ImgMetadataConverter/assets/refresh.svg";
            calculateIcon.style.width = "24px";

            deleteBtn.appendChild(deleteIcon);
            deleteTd.appendChild(deleteBtn);

            searchBtn.appendChild(searchIcon);
            searchTd.appendChild(searchBtn);

            calculateBtn.appendChild(calculateIcon);
            calculateTd.appendChild(calculateBtn);

            tr.append(resourceTd, hashTd, deleteTd, calculateTd, searchTd);

            imgMetadataConverterHashTable.appendChild(tr);
            tr = document.createElement("tr");
        }
    }
}

function buildImgMetadataConverterCivitaiResult(data, hash, img = null) {
    imgMetadataConverterHashTableCivitaiResultsContainer.innerHTML = "";

    let infoContainer = document.createElement("div");
    infoContainer.className = "model_downloader_metadatazone";
    infoContainer.style.height = "20rem";

    infoContainer.innerHTML = `
        <a href="https://civitai.com/models/${data.modelId}?modelVersionId=${data.id}" target="_blank">${escapeHtml(data.model.name)}</a>
        <br><b>Type</b>: ${escapeHtml(data.model.type)}
        <br><b>Version</b>: ${escapeHtml(data.name)}
        <br><b>Base model</b>: ${escapeHtml(data.baseModel)}
        <br><b>Published date</b>: ${escapeHtml(data.publishedAt) || "Not yet published"}
        <br><b>Version description</b>: ${data.description ? safeHtmlOnly(data.description) : "No description available"}
        <br><b>Trained words</b>: ${data.trainedWords.length > 0 ? escapeHtml(data.trainedWords.join(", ")) : "Doesn't require any trigger"}
        <br><b>Download URL</b>: <a href="${data.downloadUrl}" target="_blank">Download URL</a>
        <br><b>Download count</b>: ${data.stats.downloadCount}
        <br><b>Thumbsup count</b>: ${data.stats.thumbsUpCount}
        <br><b>File size</b>: ${fileSizeStringify(data.files.find(f => f.hashes.AutoV3 == hash.toUpperCase()).sizeKB * 1024)}
    `;

    let imageContainer = document.createElement("div");
    imageContainer.className = "model_downloader_imageside";

    if (img) {
        imageContainer.innerHTML = `<img src="${img}"/>`;
    } else {
        imageContainer.innerHTML = ``;
    }

    imgMetadataConverterHashTableCivitaiResultsContainer.append(infoContainer, imageContainer);
}

function loadImgMetadataConverterSettings() {
    genericRequest("LoadImgMetadataConverterSettings", {}, data => {
        imgMetadataConverterConfirmer.style.display = "none";
        imgMetadataConverterSettingsData.altered = {};
        imgMetadataConverterSettingsData.known = {};
        buildImgMetadataConverterSettingsMenu(data);
    });
}

function loadImgMetadataConverterHashTable(refreshStatus = true) {
    if (Object.keys(imgMetadataConverterSettingsData.altered).length > 0) {
        cancelImgMetadataConverterSettingChange();
    }

    genericRequest("LoadModelsWithImgMetadataConverterHashes", {}, data => {
        buildImgMetadataConverterHashTable(data, refreshStatus);
    });
}

function saveImgMetadataConverterSettings() {
    genericRequest("SaveImgMetadataConverterSettings", { settings: imgMetadataConverterSettingsData.altered }, data => {
        loadImgMetadataConverterSettings();
    }, 0, e => {
        showError(e);
        loadImgMetadataConverterSettings();
    });
}

function deleteImgMetadataConverterHashes(mode = "all_hashes") {
    imgMetadataConverterHashTableStatusMsg.innerHTML = "Deleting hashes, might take a while depending on the number of resources...";
    toggleImgMetadataConverterHashTableButtons(false);
    isImgMetadataConverterBussy = true;

    genericRequest("DeleteImgMetadataConverterHashes", { mode }, data => {
        displayImgMetadataConverterHashTableStatusMsg(imgMetadataConverterHashTableStatusMsg, data.message, data.success);

        if (data.success) {
            loadImgMetadataConverterHashTable(false);
        } else {
            toggleImgMetadataConverterHashTableButtons();
        }
        isImgMetadataConverterBussy = false;
    }, 0, e => {
        imgMetadataConverterHashTableStatusMsg.innerHTML = "";
        showError(e);
        loadImgMetadataConverterHashTable(false);
        isImgMetadataConverterBussy = false;
    });
}

function calculateImgMetadataConverterHash(mode) {
    imgMetadataConverterHashTableStatusMsg.innerHTML = "Calculating hashes, might take a while depending on the number of resources and your system's capabilities...";
    toggleImgMetadataConverterHashTableButtons(false);
    isImgMetadataConverterBussy = true;
    let total = 0;

    makeWSRequest("CalculateImgMetadataConverterHashWS", { mode }, data => {
        if (!data.isDone) {
            imgMetadataConverterHashTableStatusMsg.innerHTML = `Calculating ${data.count}/${data.total} ${data.modelType} hashes...`;
            total++;
        } else {
            displayImgMetadataConverterHashTableStatusMsg(imgMetadataConverterHashTableStatusMsg, mode == "overwrite" || mode == "missing" ? `Calculated ${total} hashes successfully!` : `Hash calculated for ${mode}!`, true);
            loadImgMetadataConverterHashTable(false);
            isImgMetadataConverterBussy = false;
        }
    }, 0, e => {
        imgMetadataConverterHashTableStatusMsg.innerHTML = "";
        showError(e);
        loadImgMetadataConverterHashTable(false);
        isImgMetadataConverterBussy = false;
    });
}

function imgMetadataConverterCivitaiSearch(hash) {
    imgMetadataConverterHashTableCivitaiResultMsg.innerHTML = "Searching for model on civitai, please wait...";
    imgMetadataConverterHashTableCivitaiResultMsg.scrollIntoView({ behavior: "smooth" });
    isImgMetadataConverterBussy = true;

    genericRequest("SearchModelOnCivitaiByHash", { hash }, data => {
        displayImgMetadataConverterHashTableStatusMsg(imgMetadataConverterHashTableCivitaiResultMsg, data.message, data.success);

        if (data.success) {
            if (data.data.images.length > 0) {
                imageToData(data.data.images[0].url, img => buildImgMetadataConverterCivitaiResult(data.data, hash, img));
            } else {
                buildImgMetadataConverterCivitaiResult(data.data, hash);
            }
        } else {
            imgMetadataConverterHashTableCivitaiResultsContainer.innerHTML = "";
        }
        isImgMetadataConverterBussy = false;
    }, 0, e => {
        imgMetadataConverterHashTableCivitaiResultMsg.innerHTML = "";
        imgMetadataConverterHashTableCivitaiResultsContainer.innerHTML = "";
        showError(e);
        isImgMetadataConverterBussy = false;
    });
}

function displayImgMetadataConverterHashTableStatusMsg(element, msg, success) {
    element.innerHTML = `<span class="translate" style="color: ${success ? "green" : "red"};">${success ? "Success" : "Fail"}:</span> ${msg}`;
}

function toggleImgMetadataConverterHashTableButtons(enabled = true) {
    getRequiredElementById("imgmetadataconverter-hashtable").querySelectorAll("button").forEach(button => {
        button.disabled = !enabled;
        button.style.opacity = enabled ? "1" : "0.5";
    });
}

function cancelImgMetadataConverterSettingChange() {
    for (let elementId of elementIds) {
        let element = getRequiredElementById(`imgmetadataconverter_settings_${elementId.toLowerCase()}`);

        if (element.type == "checkbox") {
            element.checked = imgMetadataConverterSettingsData.known[elementId];
        } else {
            element.value = imgMetadataConverterSettingsData.known[elementId];
        }
    }

    imgMetadataConverterConfirmer.style.display = "none";
    imgMetadataConverterSettingsData.altered = {};
}

getRequiredElementById("maintab_imgmetadataconverter").addEventListener("click", () => loadImgMetadataConverterSettings());
getRequiredElementById("imgmetadataconverterhashtablebtn").addEventListener("click", () => {
    if (!isImgMetadataConverterBussy) {
        loadImgMetadataConverterHashTable();
    }
});