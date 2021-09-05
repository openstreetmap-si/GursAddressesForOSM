#!/bin/bash
DownloadDest="${1}"
TempDest="${2}"
maxAge=720

pbfFile=${DownloadDest}"slovenia-latest.osm.pbf"
pbfMd5File=${DownloadDest}"slovenia-latest.osm.pbf.md5"

files=($pbfFile $pbfMd5File)

countTooOld=${#files[@]}
for filename in "${files[@]}"; do
	if [ $(find "${filename}" -mmin -${maxAge} | wc -l) -gt "0" ]; then
		countTooOld=$((countTooOld-1))
	fi
done

# exit if all are newer than max age
if [ "$countTooOld" -gt "0" ]; then
	echo "Need to download $countTooOld files (they are either missing or older than $maxAge minutes)"

	wget --directory-prefix "${DownloadDest}" --content-disposition -N https://download.geofabrik.de/europe/slovenia-latest.osm.pbf
	wget --directory-prefix "${DownloadDest}" --content-disposition -N https://download.geofabrik.de/europe/slovenia-latest.osm.pbf.md5
else
	echo "No need to download anything (source files are already there and not older than $maxAge minutes)"
fi

calculatedMd5=`md5 -q ${pbfFile}`
downloadedMd5=`cat ${pbfMd5File} | awk '{print $1}'`

if [ "$calculatedMd5" = "$downloadedMd5" ]; then
	echo "MD5 matched."
else
	echo "MD5 missmatch calculated MD5: '$calculatedMd5' downloaded MD5: '$downloadedMd5'."
	echo "Deleting files."
	rm ${pbfFile} ${pbfMd5File}
	echo "Re-run script."
	exit 1
fi

mkdir -p ${TempDest}

o5mFile=${TempDest}"input.o5m"
filteredO5mFile=${TempDest}"filtered.o5m"
filteredPbfFile=${TempDest}"filtered.pbf"

echo "Converting .pbf to .o5m..."
osmconvert "${pbfFile}" -o="${o5mFile}"
echo "Filtering out everything except elements with 'addr:*' or 'building'"
osmfilter "${o5mFile}" --keep="addr:*" --keep="building" --drop-author -o="${filteredO5mFile}"
echo "Converting to '${filteredPbfFile}'"
osmconvert "${filteredO5mFile}" -o="${filteredPbfFile}"

echo getFilteredPbf finished.
