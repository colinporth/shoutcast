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
    private static Stream createNewFile (String destPath, String filename, String extension) {

      // replace characters, that are not allowed in filenames. (quick and dirrrrrty ;) )
      filename = filename.Replace (":", "");
      filename = filename.Replace ("/", "");
      filename = filename.Replace ("\\", "");
      filename = filename.Replace ("<", "");
      filename = filename.Replace (">", "");
      filename = filename.Replace ("|", "");
      filename = filename.Replace ("?", "");
      filename = filename.Replace ("*", "");
      filename = filename.Replace ("\"", "");

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

      for (int i = 0; i < args.Length; i++)
        Console.WriteLine ($"Arg[{i}] = [{args[i]}]");

      String server = (args.Length > 0) ? args[0] : "http://tjc.wnyc.org/js-stream.aac";
      String serverPath = "/";

      String destPath = @"C:\shoutcast\";     // destination path for saved songs
      String destExtension = ".aac";

      Console.WriteLine (server + " " + serverPath);
      Console.WriteLine (destPath + " " + destExtension);

      // create HttpWebRequest
      HttpWebRequest request = (HttpWebRequest)WebRequest.Create (server);
      request.Headers.Add ("GET", serverPath + " HTTP/1.0");
      request.Headers.Add ("Icy-MetaData", "1"); // needed to receive metadata informations

      // execute HttpWebRequest
      HttpWebResponse response = null; // web response
      try {
        response = (HttpWebResponse)request.GetResponse();
        }
      catch (Exception ex) {
        Console.WriteLine (ex.Message);
        return;
        }
      String contentType = response.GetResponseHeader ("Content-Type");
      int metaInt = Convert.ToInt32 (response.GetResponseHeader ("icy-metaint"));
      Console.WriteLine ("Content-Type: " + contentType);
      Console.WriteLine ("icy-metaint:" + metaInt);

      Stream stream = null;
      Stream fileStream = null;
      try {
        // open stream on response
        stream = response.GetResponseStream();

        // rip stream in an endless loop
        int count = 0;
        int metadataLength = 0;
        string metadataHeader = ""; // metadata header that contains the actual songtitle
        string oldMetadataHeader = null; // previous metadata header, to compare with new header and find next song
        byte[] buffer = new byte[512]; // receive buffer

        int outBytes = 0;
        while (true) {
          // read buffer
          int bufLen = stream.Read (buffer, 0, buffer.Length);
          if (bufLen < 0)
            return;

          for (int i = 0; i < bufLen ; i++) {
            // if there is a header, the 'headerLength' would be set to a value != 0, save header to string
            if (metadataLength != 0) {
              metadataHeader += Convert.ToChar (buffer[i]);
              metadataLength--;
              if (metadataLength == 0) {
                //{{{  all metadata informations were written to the 'metadataHeader' string
                // if songtitle changes, create a new file
                if (!metadataHeader.Equals (oldMetadataHeader)) {
                  // flush and close old fileStream stream
                  if (fileStream != null) {
                    fileStream.Flush();
                    fileStream.Close();
                    Console.WriteLine ("Saved: " + outBytes);
                    }

                  // extract songtitle from metadata header. Trim was needed, because some stations don't trim the songtitle
                  string fileName = Regex.Match (metadataHeader, "(StreamTitle=')(.*)(';StreamUrl)").Groups[2].Value.Trim();

                  // write new songtitle to console for information
                  Console.WriteLine ("Saving: " + fileName);

                  // create new file with the songtitle from header and set a stream on this file
                  fileStream = createNewFile (destPath, fileName, destExtension);
                  outBytes = 0;

                  // save new header to 'oldMetadataHeader' string, to compare if there's a new song starting
                  oldMetadataHeader = metadataHeader;
                  }

                metadataHeader = "";
                }
                //}}}
              }
            else {
              //{{{  write data to file or extract metadata headerlength
              if (count++ < metaInt) {
                // write bytes to filestream
                if (fileStream != null) {
                  // as long as we don't have a songtitle, we don't open a new file and don't write any bytes
                  outBytes++;
                  fileStream.Write(buffer, i, 1);
                  }
                }
              else {
                // get headerlength from lengthbyte and multiply by 16 to get correct headerlength
                metadataLength = Convert.ToInt32 (buffer[i]) * 16;
                count = 0;
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
        if (stream != null)
          stream.Close();
        if (fileStream != null)
          fileStream.Close();
        }
      }
    }
  }
