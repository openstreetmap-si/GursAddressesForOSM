DATAFOLDER = data/
TMP = $(DATAFOLDER)temp/
TS = $$(cat $(TMP)timestamp.txt)
TSYYYY = $$(cat $(TMP)timestamp.txt | cut -b 1-4)

all:
	mkdir -p $(TMP) || true
	./getSource.sh $(TMP)

	# poor-man's git submodule:
	if [ ! -d "./GeoCoordinateConverter" ];then \
		git clone https://github.com/mrihtar/GeoCoordinateConverter ; \
	fi

	cd GeoCoordinateConverter && $(MAKE) -f Makefile.unix gk-shp

    # re-project:
	rm -r $(TMP)HS-etrs89 || true
	mkdir -p $(TMP)HS-etrs89
	./GeoCoordinateConverter/gk-shp -t 9 -dd $(TMP)HS/SI.GURS.RPE.PUB.HS.shp $(TMP)HS-etrs89/SI.GURS.RPE.PUB.HS-etrs89.shp

	#rm -r $(TMP)ko_zk_slo-etrs89 || true
	#mkdir -p $(TMP)ko_zk_slo-etrs89
	#./GeoCoordinateConverter/gk-shp -t 9 -dd $(TMP)ko_zk_slo/SI_GURS_CBZK_KO.shp $(TMP)ko_zk_slo-etrs89/SI_GURS_CBZK_KO-etrs89.shp

    # geoJson:
	mkdir -p $(DATAFOLDER)
	go run gursShp2geoJson.go


	# make a zip
	sed "s/%YYYY-MM-DD%/$(TS)/g" data-LICENSE-template.md > $(DATAFOLDER)LICENSE.md
	zip -9 -j $(DATAFOLDER)slovenia-housenumbers-$(TS).zip $(DATAFOLDER)slovenia-housenumbers.geojson $(DATAFOLDER)LICENSE.md


.PHONY: clean
clean:
	rm -r $(TMP)
	#rm -r $(TARGETFOLDER)
	if [ -d "./GeoCoordinateConverter" ];then \
		cd GeoCoordinateConverter && $(MAKE) -f Makefile.unix clean ; \
	fi

.PHONY: test
test:
	go test -v -cover

.PHONY: testShort
testShort:
	go test -v -short -cover

.PHONY: bench
bench:
	go test -cover -bench=.
