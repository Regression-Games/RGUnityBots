SCRIPTPATH="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"
read -p 'Enter the full path to the sample in your imported sample project: ' path
COPY_LOCATION="$(dirname "$SCRIPTPATH")/Samples~"
echo "Copying $path into $COPY_LOCATION"
cp -r "$path" "$COPY_LOCATION"
