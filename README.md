# Prepare addresses of Slovenia for import into OpenStreetMap

See [Slovenia Address Import project description in OpenStreetMap wiki](https://wiki.openstreetmap.org/wiki/Slovenia_Address_Import).
Do **NOT** import anything until the process is defined and approved by community!

[![Build Status](https://travis-ci.org/openstreetmap-si/GursAddressesForOSM.svg?branch=master)](https://travis-ci.org/openstreetmap-si/GursAddressesForOSM)
[![Go Report Card](https://goreportcard.com/badge/github.com/openstreetmap-si/GursAddressesForOSM)](https://goreportcard.com/report/github.com/openstreetmap-si/GursAddressesForOSM)
[![codebeat badge](https://codebeat.co/badges/ef6316aa-ab76-4f86-9c04-cac31f7942c0)](https://codebeat.co/projects/github-com-openstreetmap-si-gursaddressesforosm-master)
[![Maintainability](https://api.codeclimate.com/v1/badges/9f6ae8b5b2c751481e6c/maintainability)](https://codeclimate.com/github/openstreetmap-si/GursAddressesForOSM/maintainability)
[![codecov](https://codecov.io/gh/openstreetmap-si/GursAddressesForOSM/branch/master/graph/badge.svg)](https://codecov.io/gh/openstreetmap-si/GursAddressesForOSM)
[![Requirements Status](https://requires.io/github/openstreetmap-si/GursAddressesForOSM/requirements.svg?branch=master)](https://requires.io/github/openstreetmap-si/GursAddressesForOSM/requirements/?branch=master)


### Steps:
1. Register as user at http://egp.gu.gov.si/egp, wait for the email with the password, login
2. Run GNU `make` in this folder (requires `wget` and `go` (>1.10))
3. When prompted enter your credentials (they can be saved for later reuse)
4. Wait a minute or two for processing to finish.

### To manually download the data you should:
1. Register as user at http://egp.gu.gov.si/egp, wait for the email with the password, login
2. Expand section "10. Register prostorskih enot" / "10. Register of Spatial Units"
3. Download the data "Prostorske enote" / "Spatial units" -> `RPE_PE.ZIP` and put it in the `data/downloaded` folder
4. Download the data "Ulice" / "Streets" -> `RPE_PUL.ZIP` and put it in the `data/downloaded` folder
5. Download the data "Hišne številke" / "House numbers" -> `RPE_PE.ZIP` and put it in the `data/downloaded` folder

### Technical info:
Encoding in source shapefiles is Windows-1250 (`CP1250` in `iconv`), result is UTF8

Source shapefile structure is described in [RPE_struktura.pdf](http://www.e-prostor.gov.si/fileadmin/struktura/RPE_struktura.pdf) (only in Slovenian so far)

### Dataset source
Data can be obtained from Geodetska  uprava  Republike  Slovenije - http://egp.gu.gov.si/egp/ under CreativeCommons attribution license - [CC-BY 2.5](http://creativecommons.org/licenses/by/2.5/si/legalcode), attribution details in  [General_terms.pdf](http://www.e-prostor.gov.si/fileadmin/struktura/ANG/General_terms.pdf) (or slovene [preberi_me.pdf](http://www.e-prostor.gov.si/fileadmin/struktura/preberi_me.pdf)).

### Dependancies
1. [GeoCoordinateConverter](http://geocoordinateconverter.tk/) for most accurate reprojection (https://github.com/mrihtar/GeoCoordinateConverter will be checked out and built if it is not existing yet)
2. GNU Make, bash, wget... (normal linux stuff)
3. GoLang 1.10 or later

Note: 
* For running on Mac OS use: 
  * `gsed` instead of `sed` in `Makefile`
  * Geocoordinate branch `travis` of my fork: https://github.com/stefanb/GeoCoordinateConverter until pull request is merged upstream
* GoLang program is optimized as a Go learning exercise

### Similar import projects

* https://github.com/pnoll1/bothell_import - house numbers and building outlines
* https://github.com/SouthFLMappers/OSMImport2018 - Miami-Dade County Address + Building (+POI?) Import
* https://github.com/cascafico/MilanoHousenumbers - Milano house numbers


### TODO: 
* [ ] Use buffered channels + goroutines for concurrent processing when reading shapefile
* [X] Split into smaller files by areas (municipalities/občine, cities/naselja)
* [ ] Optional: [CGO](https://golang.org/cmd/cgo/) wrapper around GeoCoordinateConverter reprojection functionality ([geo.h](https://github.com/mrihtar/GeoCoordinateConverter/blob/master/geo.h))
* [X] Travis CI, with badges etc
* [ ] add golang linter: https://github.com/alecthomas/gometalinter
* [X] use OSM conflator (see https://wiki.openstreetmap.org/wiki/OSM_Conflator and https://github.com/mapsme/osm_conflate ) to prepare .osc files
* [X] create [`taginfo.json`](taginfo.json)
* [X] add [`taginfo.json`](https://raw.githubusercontent.com/openstreetmap-si/GursAddressesForOSM/master/taginfo.json) to https://github.com/taginfo/taginfo-projects - [PR#65](https://github.com/taginfo/taginfo-projects/pull/65) - https://taginfo.openstreetmap.org/projects/slovenia_address_import
* [ ] Find & expand abbreviated names (eg "Sv. Anton na Pohorju, Moravci v Slov. goricah") (Q: Add a tag with original, shortened value, like `short_name`?)
