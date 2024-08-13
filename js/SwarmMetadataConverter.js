function doRequest(cache = true) {
    genericRequest('SaveSwarmMetadataConverterSettings', { cache }, data => {
        console.log(data);
    });
}