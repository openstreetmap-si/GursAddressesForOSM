#!/bin/bash

BASE="data/slovenia/"
OUT=${BASE}"index.html"

cat << EOF > $OUT
<!doctype html>
<html lang="en">
<head>
    <!-- Required meta tags -->
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">
    <title>Slovenia - GURS Addresses for OpenStreetMap</title>

    <!-- Bootstrap CSS -->
    <link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/css/bootstrap.min.css" integrity="sha384-MCw98/SFnGE8fJT3GXwEOngsV7Zt27NXFoaoApmYm81iuXoPkFOJwJ8ERdknLPMO" crossorigin="anonymous">
    <link rel="stylesheet" href="https://cdn.datatables.net/1.10.18/css/dataTables.bootstrap4.min.css" crossorigin="anonymous">
</head>
<body>

<nav class="navbar navbar-expand-md navbar-dark bg-dark">
	<a class="navbar-brand" href="#">GURS Addresses for OSM</a>
	<button class="navbar-toggler" type="button" data-toggle="collapse" data-target="#navbarsExample03" aria-controls="navbarsExample03" aria-expanded="false" aria-label="Toggle navigation">
	<span class="navbar-toggler-icon"></span>
	</button>

	<div class="collapse navbar-collapse" id="navbarsExample03">
	<ul class="navbar-nav mr-auto">
		<li class="nav-item">
		<a class="nav-link" href="https://wiki.openstreetmap.org/wiki/Slovenia_Address_Import">Wiki</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://forum.openstreetmap.org/viewtopic.php?id=66162">Forum</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://taginfo.openstreetmap.org/projects/slovenia_address_import#tags">TagInfo</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://resultmaps.neis-one.org/osm-changesets?comment=GURS-HS">Changesets</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://metrics.improveosm.org/address-points/total-metrics-per-interval?duration=weekly&locationType=country&locationId=196&unit=km&from=2016-02-14&to=$(date -dlast-sunday +%Y-%m-%d)">Progress</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://github.com/openstreetmap-si/GursAddressesForOSM/">Github</a>
		</li>
	</ul>
	</div>
</nav>

<main role="main" class="pt-3 container-fluid">
<div class="alert alert-warning" role="alert">
  For evaluation purposes - do NOT import anything yet!
</div>
<nav aria-label="breadcrumb">
  <ol class="breadcrumb">
    <li class="breadcrumb-item active" aria-current="page">Slovenia</li>
  </ol>
</nav>
    <!-- Optional JavaScript -->
    <!-- jQuery first, then Popper.js, then Bootstrap JS -->
    <script src="https://code.jquery.com/jquery-3.3.1.slim.min.js" integrity="sha384-q8i/X+965DzO0rT7abK41JStQIAqVgRVzpbzo5smXKp4YfRvH+8abtTE1Pi6jizo" crossorigin="anonymous"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.14.3/umd/popper.min.js" integrity="sha384-ZMP7rVo3mIykV+2+9J3UJ46jBk0WLaUAdn689aCwoqbBJiSnjAK/l8WvCWPIPm49" crossorigin="anonymous"></script>
    <script src="https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/js/bootstrap.min.js" integrity="sha384-ChfqqxuZUCnJSK3+MXmPNIyE6ZbWh2IMqE241rYiqJxyMiZ6OW/JmZQ5stwEULTy" crossorigin="anonymous"></script>
    <script src="https://cdn.datatables.net/1.10.18/js/jquery.dataTables.min.js" crossorigin="anonymous"></script>
    <script src="https://cdn.datatables.net/1.10.18/js/dataTables.bootstrap4.min.js" crossorigin="anonymous"></script>
    <script>
	\$(document).ready( function () {
	    \$('#list').DataTable({
		    stateSave: true,
		    columnDefs: [
		        //{ targets: [10, 11, 12, 13, 14], "orderable": false},
		        { targets: [1], visible: false },
		        { targets: [2,3,4,5,6,7,8,9,10], className: 'text-right' },
		        //{ targets: '_all', visible: false }
		    ]
		});
	} );
    </script>
<table id="list" class="table table-sm table-striped table-bordered table-hover" style="width:100%">
<thead class="thead-dark sticky-top">
<tr>
<th>Municipality</th>
<th class="d-none">Cities</th>
<th>#GURS</th>
<th class="d-none d-sm-table-cell">%Conflated</th>
<th class="d-none d-lg-table-cell">#Dups</th>
<th class="d-none d-lg-table-cell">#DLed</th>
<th class="d-none d-lg-table-cell">#Upd.</th>
<th class="d-none d-lg-table-cell">#Match</th>
<th class="d-none d-lg-table-cell">#UnMatch</th>
<th class="d-none d-lg-table-cell">#Del</th>
<th class="d-none d-sm-table-cell">#Add</th>
<th>%Done</th>
</tr>
</thead>
<tbody>
EOF
#<th>#Read</th>

TOTALGURS=0
TOTALCONF=0
#TOTALREAD=0
TOTALDUPES=0
TOTALDL=0
TOTALUPD=0
TOTALMATCH=0
TOTALREMUNMATCH=0
TOTALDEL=0
TOTALADD=0

for DIRNAME in $(find data/slovenia -maxdepth 1 -mindepth 1 -type d | sort);
do
MUNDIR=$(echo "$DIRNAME" | cut -d'/' -f 3 )
MUN=$(echo "$MUNDIR" | tr "_" " " )
MUNOUT="$DIRNAME/index.html"
echo -n "Summarizing $MUN"


cat << EOF > "$MUNOUT"
<!doctype html>
<html lang="en">
<head>
    <!-- Required meta tags -->
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no">
    <title>Slovenia / $MUN - GURS Addresses for OpenStreetMap</title>

    <!-- Bootstrap CSS -->
    <link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/css/bootstrap.min.css" integrity="sha384-MCw98/SFnGE8fJT3GXwEOngsV7Zt27NXFoaoApmYm81iuXoPkFOJwJ8ERdknLPMO" crossorigin="anonymous">
    <link rel="stylesheet" href="https://cdn.datatables.net/1.10.18/css/dataTables.bootstrap4.min.css" crossorigin="anonymous">
</head>
<body>

<nav class="navbar navbar-expand-md navbar-dark bg-dark">
	<a class="navbar-brand" href="..">GURS Addresses for OSM</a>
	<button class="navbar-toggler" type="button" data-toggle="collapse" data-target="#navbarsExample03" aria-controls="navbarsExample03" aria-expanded="false" aria-label="Toggle navigation">
	<span class="navbar-toggler-icon"></span>
	</button>

	<div class="collapse navbar-collapse" id="navbarsExample03">
	<ul class="navbar-nav mr-auto">
		<li class="nav-item">
		<a class="nav-link" href="https://wiki.openstreetmap.org/wiki/Slovenia_Address_Import">Wiki</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://forum.openstreetmap.org/viewtopic.php?id=66162">Forum</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://taginfo.openstreetmap.org/projects/slovenia_address_import#tags">TagInfo</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://resultmaps.neis-one.org/osm-changesets?comment=GURS-HS">Changesets</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://metrics.improveosm.org/address-points/total-metrics-per-interval?duration=weekly&locationType=country&locationId=196&unit=km&from=2016-02-14&to=$(date -dlast-sunday +%Y-%m-%d)">Progress</a>
		</li>
		<li class="nav-item">
		<a class="nav-link" href="https://github.com/openstreetmap-si/GursAddressesForOSM/">Github</a>
		</li>
	</ul>
	</div>
</nav>

<main role="main" class="pt-3 container-fluid">
<div class="alert alert-warning" role="alert">
  For evaluation purposes - do NOT import anything yet!
</div>
<nav aria-label="breadcrumb">
  <ol class="breadcrumb">
    <li class="breadcrumb-item"><a href="..">Slovenia</a></li>
    <li class="breadcrumb-item active" aria-current="page">$MUN</li>
  </ol>
</nav>
    <!-- Optional JavaScript -->
    <!-- jQuery first, then Popper.js, then Bootstrap JS -->
    <script src="https://code.jquery.com/jquery-3.3.1.slim.min.js" integrity="sha384-q8i/X+965DzO0rT7abK41JStQIAqVgRVzpbzo5smXKp4YfRvH+8abtTE1Pi6jizo" crossorigin="anonymous"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.14.3/umd/popper.min.js" integrity="sha384-ZMP7rVo3mIykV+2+9J3UJ46jBk0WLaUAdn689aCwoqbBJiSnjAK/l8WvCWPIPm49" crossorigin="anonymous"></script>
    <script src="https://stackpath.bootstrapcdn.com/bootstrap/4.1.3/js/bootstrap.min.js" integrity="sha384-ChfqqxuZUCnJSK3+MXmPNIyE6ZbWh2IMqE241rYiqJxyMiZ6OW/JmZQ5stwEULTy" crossorigin="anonymous"></script>
    <script src="https://cdn.datatables.net/1.10.18/js/jquery.dataTables.min.js" crossorigin="anonymous"></script>
    <script src="https://cdn.datatables.net/1.10.18/js/dataTables.bootstrap4.min.js" crossorigin="anonymous"></script>
    <script>
	\$(document).ready( function () {
	    \$('#list').DataTable({
		    stateSave: true,
		    columnDefs: [
		        { targets: [11,12,13,14], "orderable": false},
		        { targets: [1,3,4,5,6,7,8,9,10], className: 'text-right' },
                        //{ targets: '_all', visible: false }
		    ]
		});
	} );
    </script>
<table id="list" class="table table-sm table-striped table-bordered table-hover" style="width:100%">
<thead class="thead-dark sticky-top">
<tr>
<th>City</th>
<th>#GURS</th>
<th class="d-none d-sm-table-cell">Conflated</th>
<th class="d-none d-lg-table-cell">#Dups</th>
<th class="d-none d-lg-table-cell">#DLed</th>
<th class="d-none d-lg-table-cell">#Upd.</th>
<th class="d-none d-lg-table-cell">#Match</th>
<th class="d-none d-lg-table-cell">#UnMatch</th>
<th class="d-none d-lg-table-cell">#Del</th>
<th class="d-none d-sm-table-cell">#Add</th>
<th>%Done</th>
<th class="d-none d-xl-table-cell">Preview</th>
<th class="d-none d-sm-table-cell">View</th>
<th class="d-none d-xl-table-cell">.osm</th>
<th class="d-none d-lg-table-cell">JOSM</th>
</tr>
</thead>
<tbody>
EOF
#<th>Municipality</th>

MUNCITIES=""
MUNTOTALGURS=0
MUNTOTALCONF=0
#MUNTOTALREAD=0
MUNTOTALDUPES=0
MUNTOTALDL=0
MUNTOTALUPD=0
MUNTOTALMATCH=0
MUNTOTALREMUNMATCH=0
MUNTOTALDEL=0
MUNTOTALADD=0

for gursGeoJson in $(find "$DIRNAME" -name '*-gurs.geojson');
do
	#DIRNAME=$(dirname $gursGeoJson); \
	BASENAME=$(basename "$gursGeoJson" -gurs.geojson)
	CITY=$(basename "$gursGeoJson" -housenumbers-gurs.geojson | tr "_" " ")
	MUNCITIES="$MUNCITIES|$CITY"
	GURSCOUNT=$(grep -c geometry "$gursGeoJson")
	TOTALGURS=$((TOTALGURS+GURSCOUNT))
	MUNTOTALGURS=$((MUNTOTALGURS+GURSCOUNT))
#	GURS="<a href='$MUNDIR/`basename $gursGeoJson`'>$GURSCOUNT</a>"
	GURS="<a href='$(basename "$gursGeoJson")'>$GURSCOUNT</a>"

	echo "<tr><td>$CITY</td><td>$GURS</td>" >> "$MUNOUT"

if [ ! -f "$DIRNAME/$BASENAME-conflate-log.txt" ]; then
    echo -n "?"
	echo "<td class='text-center'>Not yet!</td><td class='d-none d-lg-table-cell'></td><td class='d-none d-lg-table-cell'></td><td class='d-none d-lg-table-cell'></td><td class='d-none d-lg-table-cell'></td><td class='d-none d-lg-table-cell'></td><td class='d-none d-sm-table-cell'></td><td class='d-none d-lg-table-cell'></td><td class='d-none d-sm-table-cell'></td><td class='d-none d-xl-table-cell'></td><td class='d-none d-sm-table-cell'></td><td class='d-none d-xl-table-cell'></td><td class='d-none d-lg-table-cell'></td></tr>"  >> "$MUNOUT"
# <th class="d-none d-sm-table-cell">Conflated</th>
# <th class="d-none d-lg-table-cell">#Dups</th>
# <th class="d-none d-lg-table-cell">#DLed</th>
# <th class="d-none d-lg-table-cell">#Upd.</th>
# <th class="d-none d-lg-table-cell">#Match</th>
# <th class="d-none d-lg-table-cell">#UnMatch</th>
# <th class="d-none d-sm-table-cell">#Add</th>
# <th>%Done</th>
# <th class="d-none d-xl-table-cell">Preview</th>
# <th class="d-none d-sm-table-cell">View</th>
# <th class="d-none d-xl-table-cell">.osm</th>
# <th class="d-none d-lg-table-cell">JOSM</th>
        continue
fi

	TOTALCONF=$((TOTALCONF+GURSCOUNT))
	MUNTOTALCONF=$((MUNTOTALCONF+GURSCOUNT))
	LOGTS=$(stat "$DIRNAME/$BASENAME-conflate-log.txt" | grep Modify | cut -d":" -f 2-3)
	LOG="<a href='$BASENAME-conflate-log.txt'>$LOGTS</a>"
	#echo "<td><input type='button' value='Conflate' /></td>" >> $OUT
	echo "<td class=\"d-none d-sm-table-cell text-center\">$LOG</td>"  >> "$MUNOUT"

	conlog=$(cat "$DIRNAME/$BASENAME-conflate-log.txt")

	#Parse the log for:
	#Read 170 items from the dataset
	#READCOUNT=`cat $DIRNAME/$BASENAME-conflate-log.txt | grep -o -E "Read [0-9]* items from the dataset" | sed 's/[^0-9]*//g'`
	#if [ -z "READCOUNT" ]; then
	#	READCOUNT=0
	#fi
	#TOTALREAD=$(($TOTALREAD+$READCOUNT))
	#MUNTOTALREAD=$(($MUNTOTALREAD+$READCOUNT))
	#echo "<td>$READCOUNT</td>" >> $MUNOUT

	#Found 3544 duplicates in the dataset
	DUPECOUNT=$(echo "$conlog" | grep -o -E "Found [0-9]* duplicates in the dataset" | sed 's/[^0-9]*//g')
	if [ -z "$DUPECOUNT" ]; then
		DUPECOUNT=0
	fi
	TOTALDUPES=$((TOTALDUPES+DUPECOUNT))
	MUNTOTALDUPES=$((MUNTOTALDUPES+DUPECOUNT))
	echo "<td class=\"d-none d-lg-table-cell\">$DUPECOUNT</td>" >> "$MUNOUT"

	#Downloaded 0 objects from OSM
	DLCOUNT=$(echo "$conlog" | grep -o -E "Downloaded [0-9]* objects from OSM" | sed 's/[^0-9]*//g')
	if [ -z "$DLCOUNT" ]; then
		DLCOUNT=0
	fi
	TOTALDL=$((TOTALDL+DLCOUNT))
	MUNTOTALDL=$((MUNTOTALDL+DLCOUNT))
	echo "<td class=\"d-none d-lg-table-cell\">$DLCOUNT</td>" >> "$MUNOUT"

	#Updated 0 OSM objects with ref:gurs:hs_mid tag
	UPDCOUNT=$(echo "$conlog" | grep -o -E "Updated [0-9]* OSM objects with ref" | sed 's/[^0-9]*//g')
	if [ -z "$UPDCOUNT" ]; then
		UPDCOUNT=0
	fi
	TOTALUPD=$((TOTALUPD+UPDCOUNT))
	MUNTOTALUPD=$((MUNTOTALUPD+UPDCOUNT))
	echo "<td class=\"d-none d-lg-table-cell\">$UPDCOUNT</td>" >> "$MUNOUT"

	#Matched 2153 points
	MATCHCOUNT=$(echo "$conlog" | grep -o -E "Matched [0-9]* points" | sed 's/[^0-9]*//g')
	if [ -z "$MATCHCOUNT" ]; then
		MATCHCOUNT=0
	fi
	TOTALMATCH=$((TOTALMATCH+MATCHCOUNT))
	MUNTOTALMATCH=$((MUNTOTALMATCH+MATCHCOUNT))
	echo "<td class=\"d-none d-lg-table-cell\">$MATCHCOUNT</td>" >> "$MUNOUT"

	#Removed 2305 unmatched duplicates
	REMUNMATCHCOUNT=$(echo "$conlog" | grep -o -E "Removed [0-9]* unmatched duplicates" | sed 's/[^0-9]*//g')
	if [ -z "$REMUNMATCHCOUNT" ]; then
		REMUNMATCHCOUNT=0
	fi
	TOTALREMUNMATCH=$((TOTALREMUNMATCH+REMUNMATCHCOUNT))
	MUNTOTALREMUNMATCH=$((MUNTOTALREMUNMATCH+REMUNMATCHCOUNT))
	echo "<td class=\"d-none d-lg-table-cell\">$REMUNMATCHCOUNT</td>" >> "$MUNOUT"

	#Deleted 87 and retagged 0 unmatched objects from OSM
	DELCOUNT=$(echo "$conlog" | grep -o -E "Deleted [0-9]* and retagged" | sed 's/[^0-9]*//g')
	if [ -z "$DELCOUNT" ]; then
		DELCOUNT=0
	fi
	TOTALDEL=$((TOTALDEL+DELCOUNT))
	MUNTOTALDEL=$((MUNTOTALDEL+DELCOUNT))
	echo "<td class=\"d-none d-lg-table-cell\">$DELCOUNT</td>"  >> "$MUNOUT"

	#Adding 170 unmatched dataset points
	ADDCOUNT=$(echo "$conlog" | grep -o -E "Adding [0-9]* unmatched dataset points" | sed 's/[^0-9]*//g')
	if [ -z "$ADDCOUNT" ]; then
		ADDCOUNT=0
	fi
	TOTALADD=$((TOTALADD+ADDCOUNT))
	MUNTOTALADD=$((MUNTOTALADD+ADDCOUNT))
	echo "<td class=\"d-none d-sm-table-cell\">$ADDCOUNT</td>"  >> "$MUNOUT"

	PERCENT=$((100*(UPDCOUNT+MATCHCOUNT)/GURSCOUNT))
	echo "<td>$PERCENT%</td>" >> "$MUNOUT"

	# Preview
	PREVIEWGJ="<a href='$BASENAME-preview.geojson'>GeoJSON</a>"
	echo "<td class=\"d-none d-xl-table-cell\">$PREVIEWGJ</td>" >> "$MUNOUT"

	# http://geojson.io/#data=data:text/x-url,https%3A%2F%2Fd2ad6b4ur7yvpq.cloudfront.net%2Fnaturalearth-3.3.0%2Fne_50m_land.geojson
	# Mapshaper alternative: https://github.com/mbloch/mapshaper/wiki/Web-Interface , eg: http://www.mapshaper.org/?files=https://rawgit.com/nvkelso/natural-earth-vector/master/110m_physical/ne_110m_land.shp,https://rawgit.com/nvkelso/natural-earth-vector/master/110m_physical/ne_110m_land.dbf
	PREVIEWGJIO="<a href='http://geojson.io/#data=data:text/x-url,https%3A%2F%2Faddr.openstreetmap.si%2F$MUNDIR%2F$BASENAME-preview.geojson'>View</a>"
	echo "<td class=\"d-none d-sm-table-cell\">$PREVIEWGJIO</td>" >> "$MUNOUT"

	OSMLINK="<a href='$BASENAME.osm'>.osm</a>"
	echo "<td class=\"d-none d-xl-table-cell\">$OSMLINK</td>" >> "$MUNOUT"

	# JOSM import - https://wiki.openstreetmap.org/wiki/JOSM/RemoteControl#import_command
	JOSMIMPORT="<a href='http://localhost:8111/import?url=https%3A%2F%2Faddr.openstreetmap.si%2F$MUNDIR%2F$BASENAME.osm'>Load</a>"
	echo "<td class=\"d-none d-lg-table-cell\">$JOSMIMPORT</td>" >> "$MUNOUT"

	echo "</tr>" >> "$MUNOUT"
	echo -n .

done #cities loop

cat << EOF >> "$MUNOUT"
</tbody>
<tfoot>
<tr>
<th>$MUN TOTAL:</th>
<th>$MUNTOTALGURS</th>
<th class="d-none d-sm-table-cell">$((100*MUNTOTALCONF/MUNTOTALGURS))%</th>
<th class="d-none d-lg-table-cell">$MUNTOTALDUPES</th>
<th class="d-none d-lg-table-cell">$MUNTOTALDL</th>
<th class="d-none d-lg-table-cell">$MUNTOTALUPD</th>
<th class="d-none d-lg-table-cell">$MUNTOTALMATCH</th>
<th class="d-none d-lg-table-cell">$MUNTOTALREMUNMATCH</th>
<th class="d-none d-lg-table-cell">$MUNTOTALDEL</th>
<th class="d-none d-sm-table-cell">$MUNTOTALADD</th>
<th>$((100*(MUNTOTALUPD+MUNTOTALMATCH)/MUNTOTALGURS))%</th>
<th class="d-none d-xl-table-cell"></th>
<th class="d-none d-sm-table-cell"></th>
<th class="d-none d-xl-table-cell"></th>
<th class="d-none d-lg-table-cell"></th>
</tr>

</tfoot>
</table>
</main>

<footer class="footer">
	<div class="container-fluid py-1 mt-3 mb-0 bg-light">
		<small class="text-secondary text-center">
			Data &copy; <a href="http://www.gu.gov.si">GURS</a> &amp; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>, $(date)
		</small>
	</div>
</footer>

</body>
</html>
EOF

cat << EOF >> $OUT
<tr>
<td><a href="$MUNDIR/">$MUN</a></td>
<td class="d-none">$MUNCITIES</td>
<td>$MUNTOTALGURS</td>
<td class="d-none d-sm-table-cell">$((100*MUNTOTALCONF/MUNTOTALGURS))%</td>
<td class="d-none d-lg-table-cell">$MUNTOTALDUPES</td>
<td class="d-none d-lg-table-cell">$MUNTOTALDL</td>
<td class="d-none d-lg-table-cell">$MUNTOTALUPD</td>
<td class="d-none d-lg-table-cell">$MUNTOTALMATCH</td>
<td class="d-none d-lg-table-cell">$MUNTOTALREMUNMATCH</td>
<td class="d-none d-lg-table-cell">$MUNTOTALDEL</td>
<td class="d-none d-sm-table-cell">$MUNTOTALADD</td>
<td>$((100*(MUNTOTALUPD+MUNTOTALMATCH)/MUNTOTALGURS))%</td>
</tr>
EOF

echo done.
done #municipaities loop

cat << EOF >> $OUT
</tbody>
<tfoot>
<tr>
<th>Slovenia TOTAL:</th>
<th></th>
<th>$TOTALGURS</th>
<th class="d-none d-sm-table-cell">$((100*TOTALCONF/TOTALGURS))%</th>
<th class="d-none d-lg-table-cell">$TOTALDUPES</th>
<th class="d-none d-lg-table-cell">$TOTALDL</th>
<th class="d-none d-lg-table-cell">$TOTALUPD</th>
<th class="d-none d-lg-table-cell">$TOTALMATCH</th>
<th class="d-none d-lg-table-cell">$TOTALREMUNMATCH</th>
<th class="d-none d-lg-table-cell">$TOTALDEL</th>
<th class="d-none d-sm-table-cell">$TOTALADD</th>
<th>$((100*(TOTALUPD+TOTALMATCH)/TOTALGURS))%</th>
</tr>

</tfoot>
</table>
</main>

<footer class="footer">
	<div class="container-fluid py-1 mt-3 mb-0 bg-light">
		<small class="text-secondary text-center">
			Data &copy; <a href="http://www.gu.gov.si">GURS</a> &amp; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>, $(date)
		</small>
	</div>
</footer>

</body>
</html>
EOF
#<td>$TOTALREAD</td>

echo done.
