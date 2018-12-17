# addressed update 
# based on https://github.com/cascafico/MilanoHousenumbers/blob/master/conflation/profile.py
# aggiunge tag source=pippo
#add_source = False
source = 'GURS'

# do not add unique reference IDs to OSM?

# aggiunge tag ref:<dataset_id>=<id del Comune di Milano (IDMASTER)>
# True -> relying only on geometric matching every time
#no_dataset_id = True
dataset_id = 'gurs:hs_mid'

# Overpass query to use when searching OSM for data
#overpass_timeout = 120 default
overpass_timeout = 300
#query = [('amenity', 'fuel'),('waterway', 'fuel')] both conditions
#query = [('amenity', 'fuel')],[('waterway', 'fuel')]  or condition
#query = [('amenity', 'fuel'),('disused:amenity','fuel')]  namespace disused and abandoned are implicit
#query = [('amenity', 'fuel'),('ref:mise','.*')] 
#query = [('addr:postcode', '33050')] 
#query = [('addr:housenumber','.*')] 
#query = [('addr:housenumber','~.*')]  e se lettera e interno non hanno stesso case?
#query = [('addr:street','~.*')]  

#query = [('addr:housenumber','~.*')]
query = [('addr:housenumber')],[('addr:street')] #or condition
#query = [('addr:housenumber'),[('addr:street'),('addr:place')]] #or condition

# parameter --osm will use indipendently generated queries, ie:
# http://overpass-turbo.eu/s/BZq
# http://overpass-turbo.eu/s/BZM (amenity=fuel and fuel:cng or fuel:lpg not "yes" 
# use wget -O manual-query.osm <http_addr obtained exporting compact query>

# attenzione, coord errate possono rendere enorme il bbox
# use openrefine for lat lon ranges
# vantaggio: fa richieste multiple ad overpass
#bbox = True

# italia
#bbox = [35.28,6.62,47.1,18.79]

# tags to replace on matched OSM objects
#master_tags = ('addr:housenumber', 'addr:street')
master_tags = ('addr:housenumber', 'addr:street', 'addr:postcode', 'addr:place', 'addr:city' )

# delete_unmatched = True cancellerebbe anche i POI con indirizzo
delete_unmatched = False
#tag_unmatched = { 'fixme':'this addr is missing from source dataset: please check in range >10meters' }


# max distance to search for a match in meters
max_distance = 10
