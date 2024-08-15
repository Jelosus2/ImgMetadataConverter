const cache = document.getElementById("imgmetadataconverter_settings_cache");
const outputDirectory = document.getElementById("imgmetadataconverter_settings_outputdirectory");
const imgMetadataConverterConfirmer = document.getElementById("imgmetadataconverter_settings_confirmer");
const imgMetadataConverterCount = document.getElementById("imgmetadataconverter_settings_edit_count");
const imgMetadataConverterError = document.getElementById("imgmetadataconverter_settings_error");

let imgMetadataConverterData = {
    known: {},
    altered: {}
};

function loadImgMetadataConverterSettings() {
    genericRequest("LoadImgMetadataConverterSettings", {}, data => {
        if (!data.success) {
            imgMetadataConverterError.value = data.error;
            imgMetadataConverterError.style.display = "block";
            cache.checked = true;
            outputDirectory.value = "Output";
            imgMetadataConverterData.known.cache = true;
            imgMetadataConverterData.known.outputDirectory = "Output"
        } else {
            imgMetadataConverterError.style.display = "none";
            cache.checked = data.cache;
            outputDirectory.value = data.outputDirectory;
            imgMetadataConverterData.known.cache = data.cache;
            imgMetadataConverterData.known.outputDirectory = data.outputDirectory;
        }
    });
}

function saveImgMetadataConverterSettings() {
    genericRequest("SaveImgMetadataConverterSettings", { cache: cache.checked, outputDirectory: outputDirectory.value }, data => {
        imgMetadataConverterConfirmer.style.display = "none";
        imgMetadataConverterData.known = {};
        imgMetadataConverterData.altered = {};
        loadImgMetadataConverterSettings();

        if (!data.success) {
            imgMetadataConverterError.value = data.error;
            imgMetadataConverterError.style.display = "block";
        } else {
            imgMetadataConverterError.style.display = "none";
        }
    });
}

function cancelImgMetadataConverterSettingChange() {
    cache.checked = imgMetadataConverterData.known.cache;
    outputDirectory.value = imgMetadataConverterData.known.outputDirectory;
    imgMetadataConverterConfirmer.style.display = "none";
    imgMetadataConverterData.altered = {};
}

document.getElementById("maintab_imgmetadataconverter").addEventListener("click", () => loadImgMetadataConverterSettings());

cache.addEventListener("input", () => {
    if (cache.checked == imgMetadataConverterData.known.cache) {
        delete imgMetadataConverterData.altered.cache;
    } else {
        imgMetadataConverterData.altered.cache = cache.checked;
    }

    let count = Object.keys(imgMetadataConverterData.altered).length;
    imgMetadataConverterCount.innerText = count;
    imgMetadataConverterConfirmer.style.display = count == 0 ? "none" : "block";
});

outputDirectory.addEventListener("input", () => {
    if (outputDirectory.value == imgMetadataConverterData.known.outputDirectory) {
        delete imgMetadataConverterData.altered.outputDirectory;
    } else {
        imgMetadataConverterData.altered.outputDirectory = outputDirectory.value;
    }

    let count = Object.keys(imgMetadataConverterData.altered).length;
    imgMetadataConverterCount.innerText = count;
    imgMetadataConverterConfirmer.style.display = count == 0 ? "none" : "block";
});