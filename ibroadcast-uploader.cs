using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Security.Cryptography;
using System.Diagnostics;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using RestSharp;

using System.Reflection;

namespace ibroadcast_uploader
{

    public class ibroadcast_uploader
    {
        private JObject userdetails = new JObject();
        private String usermail;
        private String userpasswd;
        private String userId;
        private String userToken;
        private List<String> extensions = new List<String>();
        private List<String> md5s = new List<String>();
        private List<FileInfo> mediaFilesQ = new List<FileInfo>();

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (Object sender, ResolveEventArgs argsdll) =>
            {
                String thisExe = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                System.Reflection.AssemblyName embeddedAssembly = new System.Reflection.AssemblyName(argsdll.Name);
                String resourceName = thisExe + "." + embeddedAssembly.Name + ".dll";

                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    Byte[] assemblyData = new Byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return System.Reflection.Assembly.Load(assemblyData);
                }
            };

            ibroadcast_uploader uploader = new ibroadcast_uploader(args);

            uploader.login();
            uploader.getMD5();
            uploader.mediaFilesQ = uploader.loadMediaFilesQ(Environment.CurrentDirectory);
            uploader.executeOptions();
        }

        public ibroadcast_uploader(string[] args)
        {
            this.usage(args);
            this.initUserdetails();
        }

        private void usage(String[] args)
        {
            if (2 != args.Length)
            {
                Console.WriteLine("Run this script in the parent directory of your music files.\nusage: {0} <email_address> <password>", Process.GetCurrentProcess().ProcessName);
                Environment.Exit(-1);
            }
            usermail = args[0];
            userpasswd = args[1];
        }

        private void initUserdetails()
        {
            userdetails.Add("mode", "status");
            userdetails.Add("email_address", usermail);
            userdetails.Add("password", userpasswd);
            userdetails.Add("version", 1);
            userdetails.Add("client", "C# upload client");
            userdetails.Add("supported_types", 1);
        }

        private void login()
        {
            try
            {
                Console.WriteLine("Logging in...");

                var client = new RestClient("https://json.ibroadcast.com/s/JSON/");
                RestRequest request = new RestRequest(Method.POST);
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("text/json", userdetails, ParameterType.RequestBody);

                var response = client.Execute(request);

                if (System.Net.HttpStatusCode.OK != response.StatusCode)
                {
                    Console.WriteLine("{0} failed.\nresponse.Code: {1}\nresponse.StatusDescription: {2}", System.Reflection.MethodBase.GetCurrentMethod().Name, response.StatusCode, response.StatusDescription);
                    Environment.Exit(-1);
                }

                bool result = false;
                dynamic dynJson = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content);

                result = (bool)dynJson.result;
                if (!result)
                {
                    Console.WriteLine("{0}", (String) dynJson.message);
                    Environment.Exit(-1);
                }

                userId = (String)dynJson.user.id;
                userToken = (String)dynJson.user.token;
                Newtonsoft.Json.Linq.JArray supported = dynJson.supported;

                Console.WriteLine("Login successful"); 

                foreach (JObject jObj in supported)
                {
                    dynamic dynObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jObj.ToString());
                    String ext = (String)dynObj.extension;
                    if (!extensions.Contains(ext)) //is extension unique? (ex: .flac x 3 in response)
                    {
                        extensions.Add(ext);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} failed. Please check your email address, password combination. Exception: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.Message);
                Environment.Exit(-1);
            }

        }

        private void getMD5()
        {
            try
            {
                var client = new RestClient("https://sync.ibroadcast.com");
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("user_id", userId);
                request.AddParameter("token", userToken);

                var response = client.Execute(request);

                if (System.Net.HttpStatusCode.OK != response.StatusCode)
                {
                    Console.WriteLine("{0} failed.\nresponse.Code: {1}\nresponse.StatusDescription: {2}", 
                                      System.Reflection.MethodBase.GetCurrentMethod().Name, response.StatusCode, response.StatusDescription);
                    Environment.Exit(-1);
                }

                bool result = false;
                dynamic dynJson = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content);

                result = (bool)dynJson.result;
                if (!result)
                {
                    Console.WriteLine("{0}", (String)dynJson.message);
                    Environment.Exit(-1);
                }

                Newtonsoft.Json.Linq.JArray jsonArray = dynJson.md5;

                foreach (JValue jVal in jsonArray)
                {
                    md5s.Add((String)jVal.Value);
                }
            }

            catch (Exception e)
            {
                Console.WriteLine("{0} failed. Exception: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.Message);
                Environment.Exit(-1);
            }
        }

        private List<FileInfo> loadMediaFilesQ(String dir)
        {
            List<FileInfo> mediaFiles = null;
            try
            {
                mediaFiles = (from file in new DirectoryInfo(dir).EnumerateFiles("*.*", SearchOption.AllDirectories)
                              where extensions.Contains(file.Extension.ToLower())
                              select file).ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} failed. Exception: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.Message);
                Environment.Exit(-1);
            }

            return mediaFiles;
        }

        private void executeOptions()
        {
            try
            {
                Console.WriteLine("\nFound {0} files. Press 'L' for listing and 'U' for uploading", mediaFilesQ.Count);
                String option = Console.ReadLine();

                if (option.ToUpper().StartsWith("L"))
                {
                    Console.WriteLine("\nListing found, supported files:");
                    foreach (FileInfo file in mediaFilesQ)
                    {
                        Console.WriteLine(" - " + file.FullName);
                    }
                    Console.WriteLine("\nPress 'U' to start the upload if this looks reasonable");
                    option = Console.ReadLine();
                }

                if (option.ToUpper().StartsWith("U"))
                {
                    Console.WriteLine("Starting upload");

                    String cksum;
                    int nrUploadedFiles = 0;
                    foreach (FileInfo file in mediaFilesQ)
                    {
                        Console.WriteLine("Uploading {0}", file.Name);

                        cksum = GetMD5HashFromFile(file.FullName);
                        if (md5s.Contains(cksum))
                        {
                            Console.WriteLine("skipping, already uploaded");
                            continue;
                        }

                        if (uploadMediaFile(file))
                        {
                            nrUploadedFiles++;
                        }
                    }
                    Console.WriteLine("\nDone. {0} files were uploaded.", nrUploadedFiles);
                }
                else
                {
                    Console.WriteLine("Aborted.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} failed. Exception: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.Message);
                Environment.Exit(-1);
            }
        }

        private bool uploadMediaFile(FileInfo file)
        {
            try
            {
                byte[] data = File.ReadAllBytes(file.FullName);
                var client = new RestClient("https://sync.ibroadcast.com");

                RestRequest request = new RestRequest(Method.POST);
                request.AlwaysMultipartFormData = true;
                request.AddParameter("user_id", userId);
                request.AddParameter("token", userToken);
                request.AddParameter("file_path", file.FullName);
                request.AddParameter("method", "python uploader script");
                request.AddFile("file", data, file.Name);

                var response = client.Execute(request);

                if (System.Net.HttpStatusCode.OK != response.StatusCode)
                {
                    Console.WriteLine("uploadMediaFile{0} Failed.\nresponse.Code: {1}\nresponse.StatusDescription: {2}\nresponse.ErrorMessage: {3}", 
                                       file.Name, response.StatusCode, response.StatusDescription, response.ErrorMessage);
                    return false;
                }

                bool result = false;
                dynamic dynJson = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response.Content);

                result = (bool)dynJson.result;

                Console.WriteLine("{0}", (String)dynJson.message);
                return result;

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed! Exception: {0}", e.Message);
            }
            return false;
        }

        private static string GetMD5HashFromFile(string fileName)
        {
            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} failed. Exception: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.Message);
                Environment.Exit(-1);
            }

            return String.Empty;
        }

    }
}
