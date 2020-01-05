// shoutcast.cs
//{{{  using
using System;

using System.IO;
using System.Net;

using System.Text.RegularExpressions;
//}}}

namespace shoutcast {
  //{{{  summary
  /// <summary>
  /// Zusammenfassung für StreamDownload.
  /// </summary>
  //}}}
  public class shoutcast {
    private shoutcast() {}
    //{{{
    /// <summary>
    /// Create new file without overwritin existing files with the same filename.
    /// </summary>
    /// <param name="destPath">destination path of the new file</param>
    /// <param name="filename">filename of the file to be created</param>
    /// <returns>an output stream on the file</returns>
    private static Stream createFile (String destPath, String filename, String extension) {

      // replace characters, that are not allowed in filenames. (quick and dirrrrrty ;) )
      filename = filename.Replace (":", "");
      filename = filename.Replace ("/", "");
      filename = filename.Replace ("\\", "");
      filename = filename.Replace ("<", "");
      filename = filename.Replace (">", "");
      filename = filename.Replace ("|", "");
      filename = filename.Replace ("?", "");
      filename = filename.Replace ("*", "");
      filename = filename.Replace (",", "");
      filename = filename.Replace (".", "");

      try {
        // create directory, if it doesn't exist
        if (!Directory.Exists (destPath))
          Directory.CreateDirectory (destPath);

        // create new file
        if (!File.Exists (destPath + filename + extension))
          return File.Create (destPath + filename + extension);
        else
          // if file already exists, create a new file named <filename>(i)
          for (int i = 1; ; i++)
            if (!File.Exists (destPath + filename + "(" + i + ")" + extension))
              return File.Create (destPath + filename + "(" + i + ")" + extension);
        }
      catch (IOException) {
        return null;
        }
      }
    //}}}

    [STAThread]static void Main (string[] args) {

      String url = (args.Length > 0) ? args[0] : "http://tjc.wnyc.org/js-stream.aac";
      Console.WriteLine ("url: " + url);

      // form httpWebRequest
      HttpWebRequest request = (HttpWebRequest)WebRequest.Create (url);
      request.Headers.Add ("GET", "/" + " HTTP/1.0");
      request.Headers.Add ("Icy-MetaData", "1"); // enable metadata embedded in stream
      //{{{  send request, getResponse, form path and extension
      // getRespose
      HttpWebResponse response = null;
      try {
        response = (HttpWebResponse)request.GetResponse();
        }
      catch (Exception ex) {
        Console.WriteLine (ex.Message);
        return;
        }

      String contentType = response.GetResponseHeader ("Content-Type");
      Console.WriteLine ("Content-Type: " + contentType);

      String icyDescription = response.GetResponseHeader ("icy-description");
      Console.WriteLine ("icy-description: " + icyDescription);

      int metaInt = Convert.ToInt32 (response.GetResponseHeader ("icy-metaint"));
      Console.WriteLine ("icy-metaint: " + metaInt);

      icyDescription = icyDescription.Replace (":", "");
      icyDescription = icyDescription.Replace ("/", "");
      icyDescription = icyDescription.Replace ("\\", "");
      icyDescription = icyDescription.Replace ("<", "");
      icyDescription = icyDescription.Replace (">", "");
      icyDescription = icyDescription.Replace ("|", "");
      icyDescription = icyDescription.Replace ("?", "");
      icyDescription = icyDescription.Replace ("*", "");
      icyDescription = icyDescription.Replace (",", "");
      icyDescription = icyDescription.Replace (".", "");
      String destPath = @"C:\shoutcast\" + icyDescription + @"\";

      String destExtension = ".unknown";
      if (contentType == "audio/mpeg")
        destExtension = ".mp3";
      else if (contentType == "audio/aacp")
        destExtension = ".aac";
      else if (contentType == "audio/aac")
        destExtension = ".aac";
      Console.WriteLine ("Saving to " + destPath + "title" + destExtension);
      //}}}

      Stream httpStream = null;
      Stream fileStream = null;
      try {
        httpStream = response.GetResponseStream();

        int metadataCount = 0;
        int metadataLength = 0;
        string metadataHeader = ""; // metadata header that contains the actual songtitle
        string oldMetadataHeader = null; // previous metadata header, to compare with new header and find next song

        int headerState = 0;
        int outBytes = 0;
        int skipBytes = 0;
        byte[] buffer = new byte[512];
        while (true) {
          // read buffer
          int bufferLength = httpStream.Read (buffer, 0, buffer.Length);
          if (bufferLength < 0)
            return;

          for (int i = 0; i < bufferLength ; i++) {
            // if there is a header, the 'headerLength' would be set to a value != 0, save header to string
            if (metadataLength != 0) {
              metadataHeader += Convert.ToChar (buffer[i]);
              metadataLength--;
              if (metadataLength == 0) {
                //{{{  all metadata written to the 'metadataHeader' string
                Console.WriteLine ("metadata: " + metadataHeader);
                if (!metadataHeader.Equals (oldMetadataHeader)) {
                  // if songtitle changes, create a new file flush and close old fileStream stream
                  if (fileStream != null) {
                    fileStream.Flush();
                    fileStream.Close();
                    Console.WriteLine ("Saved: " + outBytes);
                    }

                  // extract songtitle from metadata header. Trim was needed, because some stations don't trim the songtitle
                  Match match = Regex.Match (metadataHeader, "(StreamTitle=')(.*)(';StreamUrl)");
                  string fileName = match.Groups[2].Value.Trim();

                  // write new songtitle to console for information
                  Console.WriteLine ("Saving: " + fileName);

                  // create new file with the songtitle from header and set a stream on this file
                  fileStream = createFile (destPath, fileName, destExtension);
                  outBytes = 0;
                  headerState = 0;

                  // save new header to 'oldMetadataHeader' string, to compare if there's a new song starting
                  oldMetadataHeader = metadataHeader;
                  }

                metadataHeader = "";
                }
                //}}}
              }
            else {
              //{{{  write data to file or extract metadata headerlength
              if (metadataCount++ < metaInt) {
                // write bytes to filestream
                if (fileStream != null) {
                  // as long as we don't have a songtitle, we don't open a new file and don't write any bytes
                  if (headerState == 0) {
                    // waiting for header
                    if (buffer[i] == 0xFF)
                      headerState = 1;
                    else
                      skipBytes++;
                    }
                  else if (headerState == 1) {
                    // waiting for 2nd header byte
                    if (buffer[i] >> 5 == 0x07) {
                      // 2nd header byte
                      headerState = 2;

                      fileStream.WriteByte (0xFF);
                      fileStream.Write (buffer, i, 1);

                      Console.WriteLine ("skipped bytes to header " + skipBytes);
                      skipBytes = 0;
                      }
                    else {
                      headerState = 0;
                      skipBytes++;
                      }
                    }
                  else {
                    outBytes++;
                    fileStream.Write (buffer, i, 1);
                    }
                  }
                }
              else {
                // get headerlength from lengthbyte and multiply by 16 to get correct headerlength
                metadataLength = Convert.ToInt32 (buffer[i]) * 16;
                metadataCount = 0;
                }
              }
              //}}}
            }
          }
        }
      catch (Exception ex) {
        Console.WriteLine ("shoutcast failed " + ex.Message);
        }
      finally {
        if (httpStream != null)
          httpStream.Close();
        if (fileStream != null)
          fileStream.Close();
        }
      }
    }
  }
