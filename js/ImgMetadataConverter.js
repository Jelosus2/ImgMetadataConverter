const imgMetadataConverterConfirmer = document.getElementById("imgmetadataconverter_settings_confirmer");
const imgMetadataConverterCount = document.getElementById("imgmetadataconverter_settings_edit_count");

let imgMetadataConverterData = {
    known: {},
    altered: {}
};

let elementIds = ["active", "cache", "outputDirectory", "skipDuplicates", "appendOutPathBuild"];
function buildImgMetadataConverterSettingsMenu(data) {
    for (let elementId of elementIds) {
        let element = document.getElementById(`imgmetadataconverter_settings_${elementId.toLowerCase()}`);

        if (element.type == "checkbox") {
            if (data[elementId] == undefined) {
                imgMetadataConverterData.altered[elementId] = element.checked;
            } else {
                element.checked = data[elementId];
            }
        } else {
            if (data[elementId] == undefined) {
                imgMetadataConverterData.altered[elementId] = element.value;
            } else {
                element.value = data[elementId];
            }
        }
        imgMetadataConverterData.known[elementId] = data[elementId];

        if (Object.keys(imgMetadataConverterData.altered).length > 0) {
            saveImgMetadataConverterSettings();
        }

        element.addEventListener("input", () => {
            let value = "";

            if (element.type == 'checkbox') {
                value = element.checked;
            } else {
                value = element.value;
            }

            if (value == imgMetadataConverterData.known[elementId]) {
                delete imgMetadataConverterData.altered[elementId];
            } else {
                imgMetadataConverterData.altered[elementId] = value;
            }

            let count = Object.keys(imgMetadataConverterData.altered).length;
            imgMetadataConverterCount.innerText = count;
            imgMetadataConverterConfirmer.style.display = count == 0 ? "none" : "block";
        });
    }
}

function loadImgMetadataConverterSettings() {
    genericRequest("LoadImgMetadataConverterSettings", {}, data => {
        imgMetadataConverterConfirmer.style.display = "none";
        imgMetadataConverterData.altered = {};
        imgMetadataConverterData.known = {};
        buildImgMetadataConverterSettingsMenu(data);
    });
}

function saveImgMetadataConverterSettings() {
    genericRequest("SaveImgMetadataConverterSettings", { settings: imgMetadataConverterData.altered }, data => {
        loadImgMetadataConverterSettings();
    });
}

function cancelImgMetadataConverterSettingChange() {
    for (let elementId of elementIds) {
        let element = document.getElementById(`imgmetadataconverter_settings_${elementId.toLowerCase()}`);

        if (element.type == "checkbox") {
            element.checked = imgMetadataConverterData.known[elementId];
        } else {
            element.value = imgMetadataConverterData.known[elementId];
        }
    }

    imgMetadataConverterConfirmer.style.display = "none";
    imgMetadataConverterData.altered = {};
}

document.getElementById("maintab_imgmetadataconverter").addEventListener("click", () => loadImgMetadataConverterSettings());