#!/bin/bash

tempFolder="data/temp/"
downloadFolder="data/download/"
outputFolder="data/output/"

./getSource.sh $downloadFolder $tempFolder
./getFilteredPbf.sh $downloadFolder $tempFolder

dotnet run --project OsmGursBuildingImport/OsmGursBuildingImport.csproj

# extract all OSM data for each polygon this is how xxxxx.original.osm is created
find "${tempFolder}osmium/" -name "*.json" | xargs -I % osmium extract --config % "${downloadFolder}slovenia-latest.osm.pbf"

# bz2 all .osm files so we get 90% files reduction...
find $outputFolder -name "*.osm" | xargs -P6 -I % bzip2 % 

# Install Azure CLI tools
# brew install azure-cli
# Set AZURE_STORAGE_KEY enviorment variable to authenticate uploading
# see https://docs.microsoft.com/en-us/azure/storage/blobs/authorize-data-operations-cli
storageAccount="osmstorage" # change to your storage account name
blobContainerName="gurs-import" # change to your container name

az storage blob upload-batch --account-key -d $blobContainerName --account-name $storageAccount -s "${outputFolder}" --pattern *.bz2