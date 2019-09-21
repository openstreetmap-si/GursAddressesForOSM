unexport GOFLAGS
SHELL:=/bin/bash
DATAFOLDER = data/
DLFOLDER = $(DATAFOLDER)downloaded/
TMP = $(DATAFOLDER)temp/
TS = $$(cat $(TMP)timestamp.txt)
TSYYYY = $$(cat $(TMP)timestamp.txt | cut -b 1-4)

all: download reproject geojson conflate reconflate reconflate summary

.PHONY: travis
travis: download reproject geojson # conflate summary

.PHONY: download
download:
	mkdir -p $(TMP) || true
	./getSource.sh $(DLFOLDER) $(TMP)

.PHONY: reproject
reproject:
	#reproject from d96 to epsg4326
	rm -rf $(TMP)HS-epsg4326
	ogr2ogr -progress -t_srs "EPSG:4326" $(TMP)HS-epsg4326 $(TMP)HS -nln HS-epsg4326


.PHONY: geojson
geojson:
	mkdir -p $(DATAFOLDER)
	go run gursShp2geoJson.go


	# make a zip
	sed "s/%YYYY-MM-DD%/$(TS)/g" data-LICENSE-template.md > $(DATAFOLDER)LICENSE.md
	#zip -9 -q -r $(DATAFOLDER)slovenia-housenumbers-$(TS).zip $(DATAFOLDER)slovenia/* $(DATAFOLDER)LICENSE.md


.PHONY: clean
clean:
	rm -r $(TMP)
	#rm -r $(DLFOLDER)
	#rm -r $(DATAFOLDER)
	rm -rf venv

.PHONY: test
test:
	go test -v -cover -race -coverprofile=coverage.txt -covermode=atomic

.PHONY: benchNoData
benchNoData:
	go test -v -short -cover -race -coverprofile=coverage.txt -covermode=atomic -bench=.

.PHONY: bench
bench:
	go test -cover -race -coverprofile=coverage.txt -covermode=atomic -bench=.

requirements: requirements.txt.out
	# install requirements if requirements.txt.out is missing or older than requirements.txt

requirements.txt.out: venv requirements.txt
	# install the requirements into virtual environments and record the action to requirements.txt.out
	source venv/bin/activate && pip install -r requirements.txt | tee requirements.txt.out

.PHONY: conflate
conflate: requirements
	source venv/bin/activate; \
	for gursGeoJson in $$(find data/slovenia -name '*-gurs.geojson' | sort); \
	do \
		DIRNAME=$$(dirname $$gursGeoJson); \
		BASENAME=$$(basename $$gursGeoJson -gurs.geojson); \
		echo "***** Conflating: $$DIRNAME/$$BASENAME *****"; \
		conflate -i $$gursGeoJson -v -c $$DIRNAME/$$BASENAME-preview.geojson -o $$DIRNAME/$$BASENAME.osm gursAddressesConflationProfile.py --verbose 2>&1 | tee $$DIRNAME/$$BASENAME-conflate-log.txt; \
		sleep 0.3s ;\
	done

.PHONY: reconflate
reconflate: requirements
	#fgrep --include \*-conflate-log.txt -lri "Failed to download data from Overpass API" data/slovenia/
	#or log missing!
	source venv/bin/activate; \
	for badLog in $$(fgrep --include \*-conflate-log.txt -lri "Failed to download data from Overpass API" data/slovenia/ | sort); \
	do \
		echo "***** Conflating: $$badLog *****"; \
		DIRNAME=$$(dirname $$badLog); \
		BASENAME=$$(basename $$badLog -conflate-log.txt); \
		echo "***** Conflating: $$DIRNAME/$$BASENAME *****"; \
		conflate -i "$$DIRNAME/$$BASENAME-gurs.geojson" -v -c $$DIRNAME/$$BASENAME-preview.geojson -o $$DIRNAME/$$BASENAME.osm gursAddressesConflationProfile.py --verbose 2>&1 | tee $$DIRNAME/$$BASENAME-conflate-log.txt; \
		sleep 0.3s ;\
	done

.PHONY: summary
summary:
	cp OSM_Slovenia_Logo.ico $(DATAFOLDER)/slovenia/favicon.ico
	./summarize.sh

venv:
	# basic setup
	pip install virtualenv
	virtualenv -p `which python3` venv
