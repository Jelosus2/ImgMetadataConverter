const cache = document.getElementById("imgmetadataconverter_settings_cache");
const outputDirectory = document.getElementById("imgmetadataconverter_settings_outputdirectory");
const imgMetadataConverterConfirmer = document.getElementById("imgmetadataconverter_settings_confirmer");
const imgMetadataConverterCount = document.getElementById("imgmetadataconverter_settings_edit_count");

let imgMetadataConverterData = {
    known: {},
    altered: {}
};

function loadImgMetadataConverterSettings() {
    genericRequest('LoadImgMetadataConverterSettings', {}, data => {
        cache.checked = data.cache;
        outputDirectory.value = data.outputDirectory;
        imgMetadataConverterData.known.cache = data.cache;
        imgMetadataConverterData.known.outputDirectory = data.outputDirectory;
    });
}

function saveImgMetadataConverterSettings() {
    genericRequest('SaveImgMetadataConverterSettings', { cache: cache.checked, outputDirectory: outputDirectory.value }, data => {
        imgMetadataConverterConfirmer.style.display = 'none';
        imgMetadataConverterData.known = {};
        imgMetadataConverterData.altered = {};
        loadImgMetadataConverterSettings();
    });
}

function cancelImgMetadataConverterSettingChange() {
    cache.checked = imgMetadataConverterData.known.cache;
    outputDirectory.value = imgMetadataConverterData.known.outputDirectory;
    imgMetadataConverterConfirmer.style.display = 'none';
    imgMetadataConverterData.altered = {};
}

document.getElementById("maintab_imgmetadataconverter").addEventListener('click', () => loadImgMetadataConverterSettings());

cache.addEventListener('input', () => {
    if (cache.checked == imgMetadataConverterData.known.cache) {
        delete imgMetadataConverterData.altered.cache;
    } else {
        imgMetadataConverterData.altered.cache = cache.checked;
    }

    let count = Object.keys(imgMetadataConverterData.altered).length;
    imgMetadataConverterCount.innerText = count;
    imgMetadataConverterConfirmer.style.display = count == 0 ? 'none' : 'block';
});

outputDirectory.addEventListener('input', () => {
    if (outputDirectory.value == imgMetadataConverterData.known.outputDirectory) {
        delete imgMetadataConverterData.altered.outputDirectory;
    } else {
        imgMetadataConverterData.altered.outputDirectory = outputDirectory.value;
    }

    let count = Object.keys(imgMetadataConverterData.altered).length;
    imgMetadataConverterCount.innerText = count;
    imgMetadataConverterConfirmer.style.display = count == 0 ? 'none' : 'block';
});