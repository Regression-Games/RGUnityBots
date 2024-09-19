import sys
import os
import base64

def encode_jpg_base64(file_path):
    """
    Encode a JPG file to a base64 string.

    Args:
        file_path (str): The path to the JPG file to be encoded.

    Returns:
        int: 0 if successful, 1 if an error occurred.

    Prints:
        The base64 encoded string if successful, or an error message if failed.
    """
    if not os.path.exists(file_path):
        print(f"Error: File '{file_path}' not found.")
        return 1
    
    if not os.path.isfile(file_path):
        print(f"Error: '{file_path}' is not a file.")
        return 1

    try:
        with open(file_path, "rb") as image_file:
            encoded_string = base64.b64encode(image_file.read()).decode('utf-8')
        
        if not encoded_string:
            print("Error: Encoded string is empty.")
            return 1
        
        print(encoded_string)
        return 0
    except Exception as e:
        print(f"Error: Failed to encode the file. {str(e)}")
        return 1

if __name__ == "__main__":
    """
    Main entry point of the script.

    Usage:
        python encode_jpg_base64.py <JPG_filename>

    Args:
        JPG_filename (str): The filename of the JPG image to be encoded.

    Returns:
        int: The exit code of the script (0 for success, 1 for failure).
    """
    if len(sys.argv) != 2:
        print(f"Usage: python {sys.argv[0]} <JPG_filename>")
        sys.exit(1)
    
    sys.exit(encode_jpg_base64(sys.argv[1]))