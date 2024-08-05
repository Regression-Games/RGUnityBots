if [ $# == 0 ]; then
    echo "Usage: $0 <JPG_filename>"
    exit 1
fi

test=$( base64 -w0 $1 )

echo "$test"

exit 0