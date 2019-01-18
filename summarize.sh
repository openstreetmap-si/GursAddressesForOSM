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
<div class="container-fluid">
<h2>Slovenia - GURS Addresses for OpenStreetMap</h2>
<p><a href="https://wiki.openstreetmap.org/wiki/Slovenia_Address_Import">Slovenia Address Import</a> preparation - do NOT import anything yet!</p>
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
		        //{ targets: [10, 11, 12, 13], "orderable": false},
		        { targets: [1], visible: false },
		        { targets: [2,3,4,5,6,7,8], className: 'text-right' },
		        //{ targets: '_all', visible: false }
		    ]
		});
	} );
    </script>
<table id="list" class="table table-sm table-striped table-bordered table-hover" style="width:100%">
<thead class="thead-dark">
<tr>
<th>Municipality</th>
<th>Cities</th>
<th>#</th>
<th>#Dups</th>
<th>#DLed</th>
<th>#Upd.</th>
<th>#Match</th>
<th>#Add</th>
<th>%</th>
</tr>
</thead>
<tbody>
EOF
#<th>#Read</th>

TOTALGURS=0
#TOTALREAD=0
TOTALDUPES=0
TOTALDL=0
TOTALUPD=0
TOTALMATCH=0
TOTALADD=0

for DIRNAME in $(find data/slovenia -maxdepth 1 -mindepth 1 -type d);
do
MUNDIR=$(echo $DIRNAME | cut -d'/' -f 3 )
MUN=$(echo $MUNDIR | tr "_" " " )
MUNOUT="$DIRNAME/index.html"
echo -n Summarizing $MUN


cat << EOF > $MUNOUT
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
<div class="container-fluid">
<h2><a href="..">Slovenia</a> / $MUN - GURS Addresses for OpenStreetMap</h2>
<p><a href="https://wiki.openstreetmap.org/wiki/Slovenia_Address_Import">Slovenia Address Import</a> preparation - do NOT import anything yet!</p>
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
		        { targets: [9, 10, 11, 12], "orderable": false},
		        { targets: [1,3,4,5,6,7,8], className: 'text-right' },
                        //{ targets: '_all', visible: false }
		    ]
		});
	} );
    </script>
<table id="list" class="table table-sm table-striped table-bordered table-hover" style="width:100%">
<thead class="thead-dark">
<tr>
<th>City</th>
<th>#</th>
<th>Conflated</th>
<th>#Dups</th>
<th>#DLed</th>
<th>#Upd.</th>
<th>#Match</th>
<th>#Add</th>
<th>%</th>
<th>Preview</th>
<th>View</th>
<th>.osm</th>
<th>JOSM</th>
</tr>
</thead>
<tbody>
EOF
#<th>Municipality</th>

MUNCITIES=""
MUNTOTALGURS=0
#MUNTOTALREAD=0
MUNTOTALDUPES=0
MUNTOTALDL=0
MUNTOTALUPD=0
MUNTOTALMATCH=0
MUNTOTALADD=0

for gursGeoJson in $(find $DIRNAME -name '*-gurs.geojson');
do
	#DIRNAME=$(dirname $gursGeoJson); \
	BASENAME=$(basename $gursGeoJson -gurs.geojson)
	CITY=$(echo `basename $gursGeoJson -housenumbers-gurs.geojson` | tr "_" " ")
	MUNCITIES="$MUNCITIES|$CITY"
	GURSCOUNT=`cat $gursGeoJson | grep geometry | wc -l`
	TOTALGURS=$(($TOTALGURS+$GURSCOUNT))
	MUNTOTALGURS=$(($MUNTOTALGURS+$GURSCOUNT))
	GURS="<a href='$MUNDIR/`basename $gursGeoJson`'>$GURSCOUNT</a>"

	echo "<tr>" >> $MUNOUT

	echo "<td>$CITY</td>" >> $MUNOUT
	echo "<td>$GURS</td>" >> $MUNOUT

if [ ! -f $DIRNAME/$BASENAME-conflate-log.txt ]; then
    echo -n "?"
	echo "<td>Not yet!</td><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr>"  >> $MUNOUT
        continue
fi

	LOGTS=`stat $DIRNAME/$BASENAME-conflate-log.txt | grep Modify | cut -d":" -f 2-3`
	LOG="<a href='$BASENAME-conflate-log.txt'>$LOGTS</a>"
	#echo "<td><input type='button' value='Conflate' /></td>" >> $OUT
	echo "<td>$LOG</td>"  >> $MUNOUT

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
	DUPECOUNT=`cat $DIRNAME/$BASENAME-conflate-log.txt | grep -o -E "Found [0-9]* duplicates in the dataset" | sed 's/[^0-9]*//g'`
	if [ -z "$DUPECOUNT" ]; then
		DUPECOUNT=0
	fi
	TOTALDUPES=$(($TOTALDUPES+$DUPECOUNT))
	MUNTOTALDUPES=$(($MUNTOTALDUPES+$DUPECOUNT))
	echo "<td>$DUPECOUNT</td>" >> $MUNOUT

	#Downloaded 0 objects from OSM
	DLCOUNT=`cat $DIRNAME/$BASENAME-conflate-log.txt | grep -o -E "Downloaded [0-9]* objects from OSM" | sed 's/[^0-9]*//g'`
	if [ -z "$DLCOUNT" ]; then
		DLCOUNT=0
	fi
	TOTALDL=$(($TOTALDL+$DLCOUNT))
	MUNTOTALDL=$(($MUNTOTALDL+$DLCOUNT))
	echo "<td>$DLCOUNT</td>" >> $MUNOUT

	#Updated 0 OSM objects with ref:gurs:hs_mid tag
	UPDCOUNT=`cat $DIRNAME/$BASENAME-conflate-log.txt | grep -o -E "Updated [0-9]* OSM objects with ref" | sed 's/[^0-9]*//g'`
	if [ -z "$UPDCOUNT" ]; then
		UPDCOUNT=0
	fi
	TOTALUPD=$(($TOTALUPD+$UPDCOUNT))
	MUNTOTALUPD=$(($MUNTOTALUPD+$UPDCOUNT))
	echo "<td>$UPDCOUNT</td>" >> $MUNOUT

	#Matched 2153 points
	MATCHCOUNT=`cat $DIRNAME/$BASENAME-conflate-log.txt | grep -o -E "Matched [0-9]* points" | sed 's/[^0-9]*//g'`
	if [ -z "$MATCHCOUNT" ]; then
		MATCHCOUNT=0
	fi
	TOTALMATCH=$(($TOTALMATCH+$MATCHCOUNT))
	MUNTOTALMATCH=$(($MUNTOTALMATCH+$MATCHCOUNT))
	echo "<td>$MATCHCOUNT</td>" >> $MUNOUT

	#Adding 170 unmatched dataset points
	ADDCOUNT=`cat $DIRNAME/$BASENAME-conflate-log.txt | grep -o -E "Adding [0-9]* unmatched dataset points" | sed 's/[^0-9]*//g'`
	if [ -z "$ADDCOUNT" ]; then
		ADDCOUNT=0
	fi
	TOTALADD=$(($TOTALADD+$ADDCOUNT))
	MUNTOTALADD=$(($MUNTOTALADD+$ADDCOUNT))
	echo "<td>$ADDCOUNT</td>"  >> $MUNOUT

	PERCENT=$((100*$MATCHCOUNT/$GURSCOUNT))
	echo "<td>$PERCENT%</td>" >> $MUNOUT

	# Preview
	# http://geojson.io/#data=data:text/x-url,https%3A%2F%2Fd2ad6b4ur7yvpq.cloudfront.net%2Fnaturalearth-3.3.0%2Fne_50m_land.geojson
	# Mapshaper alternative: https://github.com/mbloch/mapshaper/wiki/Web-Interface , eg: http://www.mapshaper.org/?files=https://rawgit.com/nvkelso/natural-earth-vector/master/110m_physical/ne_110m_land.shp,https://rawgit.com/nvkelso/natural-earth-vector/master/110m_physical/ne_110m_land.dbf
	PREVIEWGJ="<a href='$BASENAME-preview.geojson'>GeoJSON</a>"
	PREVIEWGJIO="<a href='http://geojson.io/#data=data:text/x-url,https%3A%2F%2Faddr.openstreetmap.si%2F$MUNDIR%2F$BASENAME-preview.geojson'>View</a>"
	echo "<td>$PREVIEWGJ</td>" >> $MUNOUT
	echo "<td>$PREVIEWGJIO</td>" >> $MUNOUT

	# JOSM import - https://wiki.openstreetmap.org/wiki/JOSM/RemoteControl#import_command
	OSMLINK="<a href='$BASENAME.osm'>.osm</a>"
	JOSMIMPORT="<a href='http://localhost:8111/import?url=https%3A%2F%2Faddr.openstreetmap.si%2F$MUNDIR%2F$BASENAME.osm'>Import</a>"
	echo "<td>$OSMLINK</td>" >> $MUNOUT
	echo "<td>$JOSMIMPORT</td>" >> $MUNOUT

	echo "</tr>" >> $MUNOUT
	echo -n .

done #cities loop

cat << EOF >> $MUNOUT
</tbody>
<tfoot>
<tr>
<th>$MUN TOTAL:</th>
<th>$MUNTOTALGURS</th>
<th></th>
<th>$MUNTOTALDUPES</th>
<th>$MUNTOTALDL</th>
<th>$MUNTOTALUPD</th>
<th>$MUNTOTALMATCH</th>
<th>$MUNTOTALADD</th>
<th>$((100*$MUNTOTALMATCH/$MUNTOTALGURS))%</th>
<th></th>
<th></th>
<th></th>
<th></th>
</tr>

</tfoot>
</table>
Report finished on `date`
</div>
</body>
</html>
EOF

cat << EOF >> $OUT
<tr>
<td><a href="$MUNDIR/">$MUN</a></td>
<td>$MUNCITIES</td>
<td>$MUNTOTALGURS</td>
<td>$MUNTOTALDUPES</td>
<td>$MUNTOTALDL</td>
<td>$MUNTOTALUPD</td>
<td>$MUNTOTALMATCH</td>
<td>$MUNTOTALADD</td>
<td>$((100*$MUNTOTALMATCH/$MUNTOTALGURS))%</td>
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
<th>$TOTALDUPES</th>
<th>$TOTALDL</th>
<th>$TOTALUPD</th>
<th>$TOTALMATCH</th>
<th>$TOTALADD</th>
<th>$((100*$TOTALMATCH/$TOTALGURS))%</th>
</tr>

</tfoot>
</table>
Report finished on `date`
</div>
</body>
</html>
EOF
#<td>$TOTALREAD</td>

echo done.
