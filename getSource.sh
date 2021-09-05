#!/bin/bash
DownloadDest="${1}"
TempDest="${2}"
credentialsFile="CREDENTIALS-egp.gu.gov.si.txt"
maxAge=720
baseUrl="https://egp.gu.gov.si/egp/"
files=("RPE_PE.ZIP" "RPE_UL.ZIP" "RPE_HS.ZIP" "KS_SLO_CSV_A_U.zip" "KS_SLO_SHP_G.zip")
#files=("RPE_PE.ZIP" "RPE_UL.ZIP" "RPE_HS.ZIP" "KS_SLO_CSV_A_U.zip" "KS_SLO_SHP_G.zip" "ko_zk_slo.zip")

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

function extractDownloaded() {
	#----- extract: -------
	for file in "${DownloadDest}"*.{zip,ZIP}; do
		extdir=$(basename "$file" .ZIP)
		extdir=$(basename "$extdir" .zip)
		echo "$extdir"
		unzip -o -d "${TempDest}$extdir" "$file"
	done
	for file in "${TempDest}"RPE_*/*.zip; do unzip -o -d "${TempDest}" "$file"; done

	$STATCMD -c '%y' "${TempDest}HS/HS.shp" | cut -d' ' -f1 >"${TempDest}timestamp.txt"
}

countTooOld=${#files[@]}

for filename in "${files[@]}"; do
	fullfilename="${DownloadDest}${filename}"
	echo $fullfilename
	if [ $(find "${fullfilename}" -mmin -${maxAge} | wc -l) -gt "0" ]; then
		countTooOld=$((countTooOld-1))
	fi
done

# exit if all are newer than max age
if [ "$countTooOld" -gt "0" ]; then
	echo "Need to download $countTooOld files (they are either missing or older than $maxAge minutes)"
else
	echo "No need to download anything (source files are already there and not older than $maxAge minutes)"
	extractDownloaded
	exit 0
fi


# Clean up leftovers from previous failed runs
rm -f "${DownloadDest}cookies.txt"
rm -f "${DownloadDest}login.html"

commonWgetParams=(--load-cookies "${DownloadDest}cookies.txt" --save-cookies "${DownloadDest}cookies.txt" --directory-prefix "${DownloadDest}" --keep-session-cookies --ca-certificate "sigov-ca2.pem")
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
	csrftoken="$($SEDCMD -n 's/.*name="_csrf"\s\+value="\([^"]\+\).*/\1/p' "${DownloadDest}login.html")"

	if [ -z "${csrftoken}" ]; then
		echo "No CSRF token found, exitting!"
		exit 1
	fi

	echo "Got CSRF token: \"${csrftoken}\"."

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
		"${baseUrl}download-file.html?id=$1&format=10&d96=1"
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

#KS_SLO_SHP_G.zip
downloadFile 191

#KS_SLO_CSV_A_U.zip, calling wget dirrectly because different format and d96
wget "${commonWgetParams[@]}" --content-disposition -N "${baseUrl}download-file.html?id=192&format=50&d96=4"

#ko_zk_slo.zip
#downloadFile 108

# Clean up secrets so they are not cached
rm -f "${DownloadDest}cookies.txt"


extractDownloaded

echo getSource finished.
