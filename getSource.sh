#!/bin/bash
dest="${1}"
credentialsFile="CREDENTIALS-egp.gu.gov.si.txt"
maxAge=720

SEDCMD=sed
STATCMD=stat
unameOut="$(uname -s)"
case "${unameOut}" in
Linux*) machine=Linux ;;
Darwin*)
	machine=Mac
	SEDCMD=gsed
	STATCMD=gstat
	;;
CYGWIN*) machine=Cygwin ;;
MINGW*) machine=MinGw ;;
*) machine="UNKNOWN:${unameOut};" ;;
esac
echo Running on: "${machine}", using $SEDCMD and $STATCMD commands

countTooOld=3

#if [ -f "${dest}RPE_PE.ZIP"  -a -f "${dest}RPE_UL.ZIP" -a -f "${dest}RPE_HS.ZIP" -a -f "${dest}ko_zk_slo.zip" ] ; then
if [ -f "${dest}RPE_PE.ZIP" ] && [ -f "${dest}RPE_UL.ZIP" ] && [ -f "${dest}RPE_HS.ZIP" ]; then
	#check age of existing files
	#countTooOld=`find ${dest}RPE_PE.ZIP ${dest}RPE_UL.ZIP ${dest}RPE_HS.ZIP ${dest}ko_zk_slo.zip -mmin +${maxAge} | wc -l`
	countTooOld=$(find "${dest}RPE_PE.ZIP" "${dest}RPE_UL.ZIP" "${dest}RPE_HS.ZIP" -mmin +${maxAge} | wc -l)
fi

# exit if all are newer than max age
if [ "$countTooOld" -gt "0" ]; then
	echo "Need to download $countTooOld files (they are either missing or older than $maxAge minutes)"
else
	echo "No need to download anything (source files are already there and not older than $maxAge minutes)"
	exit 0
fi

#------ download all:------
# read possibly existing credentials...
# shellcheck source=/dev/null
source "$credentialsFile"

echo Credentials for https://egp.gu.gov.si/egp/

if [ -z "$username" ]; then
	echo -n "	Username: "
	read -r username
	echo "username=\"$username\"" >"$credentialsFile"
else
	echo "	Username: '$username'"
fi

if [ -z "$password" ]; then
	echo -n "	Password: "
	read -r password
	read -p "	Save password in plain text to $credentialsFile for future use? (y/N) " -n 1 -r
	echo # (optional) move to a new line
	if [[ $REPLY =~ ^[Yy]$ ]]; then
		# save it only if wanted
		echo "password=\"$password\"" >>"$credentialsFile"
	fi
else
	echo "	Password: *********"
fi

# Clean up leftovers from previous runs
rm -f "${dest}cookies.txt"
rm -f "${dest}login.html"

# Log in to the server.  This can be done only once.
wget --quiet \
	--save-cookies "${dest}cookies.txt" \
	--directory-prefix "${dest}" \
	--keep-session-cookies \
	--ca-certificate=sigov-ca2.pem \
	"https://egp.gu.gov.si/egp/login.html"
# example login.html content:
# <input type="hidden" name="_csrf" value="089070ed-b40a-4e3c-ab22-422de0daffff" />

csrftoken="$($SEDCMD -n 's/.*name="_csrf"\s\+value="\([^"]\+\).*/\1/p' "${dest}login.html")"

echo Got CSRF token: "${csrftoken}".
#cat cookies.txt

loginFormData="username=${username}&password=${password}&_csrf=${csrftoken}"
#echo login form data: $loginFormData

#exit 1
wget --quiet --load-cookies "${dest}cookies.txt" \
	--save-cookies "${dest}cookies.txt" \
	--keep-session-cookies \
	--referer https://egp.gu.gov.si/egp/ \
	--post-data "${loginFormData}" \
	--delete-after \
	--ca-certificate=sigov-ca2.pem \
	"https://egp.gu.gov.si/egp/login.html"

# Now grab the data we care about.

#RPE_PE.ZIP
wget --load-cookies "${dest}cookies.txt" \
	--directory-prefix "${dest}" \
	--content-disposition -N \
	--ca-certificate=sigov-ca2.pem \
	"https://egp.gu.gov.si/egp/download-file.html?id=105&format=10&d96=0"

#RPE_UL.ZIP
wget --load-cookies "${dest}cookies.txt" \
	--directory-prefix "${dest}" \
	--content-disposition -N \
	--ca-certificate=sigov-ca2.pem \
	"https://egp.gu.gov.si/egp/download-file.html?id=106&format=10&d96=0"

#RPE_HS.ZIP
wget --load-cookies "${dest}cookies.txt" \
	--directory-prefix "${dest}" \
	--content-disposition -N \
	--ca-certificate=sigov-ca2.pem \
	"https://egp.gu.gov.si/egp/download-file.html?id=107&format=10&d96=0"

#ko_zk_slo.zip
#wget --load-cookies ${dest}cookies.txt \
#     --directory-prefix "${dest}" \
#     --content-disposition -N \
#     --ca-certificate=sigov-ca2.pem \
#     "https://egp.gu.gov.si/egp/download-file.html?id=108&format=10&d96=0"

#----- extract: -------
for file in "${dest}"RPE_*.ZIP; do
	extdir=$(basename "$file" .ZIP)
	echo "$extdir"
	unzip -o -d "${dest}$extdir" "$file"
done
for file in "${dest}"RPE_*/*.zip; do unzip -o -d "${dest}" "$file"; done

#unzip -o -d "${dest}/ko_zk_slo" "${dest}ko_zk_slo.zip"

$STATCMD -c '%y' "${dest}HS/SI.GURS.RPE.PUB.HS.shp" | cut -d' ' -f1 >"${dest}timestamp.txt"

echo getSource finished.
