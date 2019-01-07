SHELL:=/bin/bash
DATAFOLDER = data/
DLFOLDER = $(DATAFOLDER)downloaded/
TMP = $(DATAFOLDER)temp/
TS = $$(cat $(TMP)timestamp.txt)
TSYYYY = $$(cat $(TMP)timestamp.txt | cut -b 1-4)

all: download reproject geojson conflate summary

.PHONY: download
download:
	mkdir -p $(TMP) || true
	./getSource.sh $(DLFOLDER) $(TMP)

.PHONY: reproject
reproject:
	# poor-man's git submodule:
	if [ ! -d "./GeoCoordinateConverter" ];then \
		git clone https://github.com/mrihtar/GeoCoordinateConverter ; \
	fi

	cd GeoCoordinateConverter && $(MAKE) -f Makefile.unix gk-shp

    # re-project housenumbers:
	rm -r $(TMP)HS-etrs89 || true
	mkdir -p $(TMP)HS-etrs89
	./GeoCoordinateConverter/gk-shp -t 9 -dd $(TMP)HS/SI.GURS.RPE.PUB.HS.shp $(TMP)HS-etrs89/SI.GURS.RPE.PUB.HS-etrs89.shp

	#rm -r $(TMP)ko_zk_slo-etrs89 || true
	#mkdir -p $(TMP)ko_zk_slo-etrs89
	#./GeoCoordinateConverter/gk-shp -t 9 -dd $(TMP)ko_zk_slo/SI_GURS_CBZK_KO.shp $(TMP)ko_zk_slo-etrs89/SI_GURS_CBZK_KO-etrs89.shp

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
	if [ -d "./GeoCoordinateConverter" ];then \
		cd GeoCoordinateConverter && $(MAKE) -f Makefile.unix clean ; \
	fi
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
	for gursGeoJson in $$(find data/slovenia -name '*-gurs.geojson'); \
	do \
		DIRNAME=$$(dirname $$gursGeoJson); \
		BASENAME=$$(basename $$gursGeoJson -gurs.geojson); \
		echo "***** Conflating: $$DIRNAME/$$BASENAME *****"; \
		conflate -i $$gursGeoJson -v -c $$DIRNAME/$$BASENAME-preview.geojson -o $$DIRNAME/$$BASENAME.osm gursAddressesConflationProfile.py --verbose 2>&1 | tee $$DIRNAME/$$BASENAME-conflate-log.txt; \
		sleep 0.3s ;\
	done

.PHONY: summary
summary:
	./summarize.sh

venv:
	# basic setup
	pip install virtualenv
	virtualenv -p `which python3` venv
