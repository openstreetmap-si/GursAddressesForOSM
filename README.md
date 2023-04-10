# Prepare addresses of Slovenia for import into OpenStreetMap

See [Slovenia Address Import project description in OpenStreetMap wiki](https://wiki.openstreetmap.org/wiki/Slovenia_Address_Import).
Do **NOT** import anything until the process is defined and approved by community!

[![golangci-lint](https://github.com/openstreetmap-si/GursAddressesForOSM/actions/workflows/golangci-lint.yml/badge.svg)](https://github.com/openstreetmap-si/GursAddressesForOSM/actions/workflows/golangci-lint.yml)
[![Go Report Card](https://goreportcard.com/badge/github.com/openstreetmap-si/GursAddressesForOSM)](https://goreportcard.com/report/github.com/openstreetmap-si/GursAddressesForOSM)
[![codebeat badge](https://codebeat.co/badges/ef6316aa-ab76-4f86-9c04-cac31f7942c0)](https://codebeat.co/projects/github-com-openstreetmap-si-gursaddressesforosm-master)
[![Maintainability](https://api.codeclimate.com/v1/badges/9f6ae8b5b2c751481e6c/maintainability)](https://codeclimate.com/github/openstreetmap-si/GursAddressesForOSM/maintainability)

## Steps

1. Register as user at [https://egp.gu.gov.si/egp/](https://egp.gu.gov.si/egp/?lang=en), wait for the email with the password, login
2. Run GNU `make` in this folder (requires `wget` and `go` (>1.10))
3. When prompted enter your credentials (they can be saved for later reuse)
4. Wait a minute or two for processing to finish.

## To manually download the data you should

1. Register as user at [https://egp.gu.gov.si/egp/](https://egp.gu.gov.si/egp/?lang=en), wait for the email with the password, login
2. Expand section "2. Register prostorskih enot" / "2. Register of Spatial Units"
3. Download the data "Prostorske enote" / "Spatial units" -> `RPE_PE.ZIP` and put it in the `data/downloaded` folder
4. Download the data "Ulice" / "Streets" -> `RPE_PUL.ZIP` and put it in the `data/downloaded` folder
5. Download the data "Hišne številke" / "House numbers" -> `RPE_PE.ZIP` and put it in the `data/downloaded` folder

## Technical info

Encoding in source shapefiles is Windows-1250 (`CP1250` in `iconv`), result is UTF8

Source shapefile structure is described in [RPE_struktura.docx](https://www.e-prostor.gov.si/fileadmin/struktura/EGP/RPE_struktura.docx) (only in Slovenian so far)

## Dataset source

Data can be obtained from Geodetska  uprava  Republike  Slovenije - [https://egp.gu.gov.si/egp/](https://egp.gu.gov.si/egp/?lang=en) under CreativeCommons attribution license - [CC-BY 4.0](https://creativecommons.org/licenses/by/4.0), attribution details in  [General_terms.pdf](https://www.e-prostor.gov.si/fileadmin/struktura/EGP/General_terms.pdf) (or slovene [preberi_me.pdf](https://www.e-prostor.gov.si/fileadmin/struktura/EGP/preberi_me.pdf)).

## Dependancies

1. GNU Make, bash, wget... (normal linux stuff)
2. GoLang 1.10 or later (program is optimized as a Go learning exercise)

### Similar import projects

* [pnoll1/bothell_import](https://github.com/pnoll1/bothell_import) - house numbers and building outlines
* [SouthFLMappers/OSMImport2018](https://github.com/SouthFLMappers/OSMImport2018) - Miami-Dade County Address + Building (+POI?) Import
* [cascafico/MilanoHousenumbers](https://github.com/cascafico/MilanoHousenumbers) - Milano house numbers

## TODO

* [X] Split into smaller files by areas (municipalities/občine, cities/naselja)
* [X] add golang ~~[gometalinter](https://github.com/alecthomas/gometalinter)~~ [golangci-lint](https://github.com/golangci/golangci-lint) [DONE](https://github.com/openstreetmap-si/GursAddressesForOSM/commit/dcd875f7adc7ddcfb346ff213ffbafb9ce248f6a)
* [X] use [OSM conflator](https://wiki.openstreetmap.org/wiki/OSM_Conflator) [source code](https://github.com/mapsme/osm_conflate) to prepare .osc files
* [X] create [`taginfo.json`](taginfo.json)
* [X] add [`taginfo.json`](https://raw.githubusercontent.com/openstreetmap-si/GursAddressesForOSM/master/taginfo.json) to [taginfo-projects](https://github.com/taginfo/taginfo-projects) - [PR#65](https://github.com/taginfo/taginfo-projects/pull/65) - [DONE](https://taginfo.openstreetmap.org/projects/slovenia_address_import)
* [X] Find & expand abbreviated names (eg "Moravci v Slov. goricah", "Pristava pri Polh. Gradcu"). Done, see `overrides` folder. (Q: Add a tag with original, shortened value, like `short_name`?)
* [X] Convert bilingual postcode names "Piran - Pirano" -> "Piran / Pirano", keeping "Šmarje - Sap", "Ljubljana - Šmartno"...
