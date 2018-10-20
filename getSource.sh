#!/bin/bash
dest="${1}"
credentialsFile="CREDENTIALS-egp.gu.gov.si.txt"
maxAge=720
baseUrl="http://egp.gu.gov.si/egp/"

SEDCMD="sed"
STATCMD="stat"
unameOut="$(uname -s)"
case "${unameOut}" in
Linux*) machine=Linux ;;
Darwin*)
	machine="Mac"
	SEDCMD="gsed"
	STATCMD="gstat"
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


# Clean up leftovers from previous runs
rm -f "${dest}cookies.txt"
rm -f "${dest}login.html"

commonWgetParams=(--load-cookies "${dest}cookies.txt" --save-cookies "${dest}cookies.txt" --directory-prefix "${dest}" --keep-session-cookies --ca-certificate "sigov-ca2.pem")
# --no-hsts
# --quiet
# --ciphers "HIGH:!aNULL:!MD5:!RC4" \
# --secure-protocol=TLSv1 \	
# --referer "${baseUrl}" \

function prepareCredentials() {
	#------ username & password: ------
	# read possibly existing credentials...
	# shellcheck source=/dev/null
	source "$credentialsFile"

	echo Credentials for ${baseUrl}

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
}

function login() {
	#------ Log in to the server.  This can be done only once ------
	wget "${commonWgetParams[@]}" \
		--quiet \
		"${baseUrl}login.html"

	# example login.html content:
	# <input type="hidden" name="_csrf" value="089070ed-b40a-4e3c-ab22-422de0daffff" />
	csrftoken="$($SEDCMD -n 's/.*name="_csrf"\s\+value="\([^"]\+\).*/\1/p' "${dest}login.html")"

	if [ -z "${csrftoken}" ]; then
		echo "No CSRF token found, exitting!"
		exit 1
	fi

	echo "Got CSRF token: \"${csrftoken}\"."

	echo "TRAVIS=${TRAVIS}"
	if [ "${TRAVIS}" != "true" ]; then
		prepareCredentials
	else
		# TODO: use secure credentials from travis.yml
		echo "Running in TRAVIS CI, aborting for now"
		exit 1
	fi


	loginFormData="username=${username}&password=${password}&_csrf=${csrftoken}"
	#echo login form data: $loginFormData

	#exit 1
	wget "${commonWgetParams[@]}" \
		--post-data "${loginFormData}" \
		--delete-after \
		--quiet \
		"${baseUrl}login.html"
}


# pass numeric file id as parameter
function downloadFile() {
	wget "${commonWgetParams[@]}" \
		--content-disposition -N \
		"${baseUrl}download-file.html?id=$1&format=10&d96=0"
}

# ---------------------------------------------
login

#------ Download all data we care about: ------
#RPE_PE.ZIP
downloadFile 105

#RPE_UL.ZIP
downloadFile 106

#RPE_HS.ZIP
downloadFile 107

#ko_zk_slo.zip
#downloadFile 108

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
