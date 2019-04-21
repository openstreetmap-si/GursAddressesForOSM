#!/bin/bash
source venv/bin/activate
#for gursGeoJson in $$(find data/slovenia -name '*-gurs.geojson');
#do
gursGeoJson=$1
        DIRNAME=$(dirname "$gursGeoJson")
        BASENAME=$(basename "$gursGeoJson" -gurs.geojson)
        echo "***** Conflating: $$DIRNAME/$$BASENAME *****"
        conflate -i "$gursGeoJson" -v -c "$DIRNAME/$BASENAME-preview.geojson" -o "$DIRNAME/$BASENAME.osm" gursAddressesConflationProfile.py --verbose 2>&1 | tee "$DIRNAME/$BASENAME-conflate-log.txt"
#done

