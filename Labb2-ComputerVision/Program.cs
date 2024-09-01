using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System.Diagnostics;



namespace Labb2_ComputerVision
{
    class Program
    {
       
        private static ComputerVisionClient cvClient;
        static async Task Main(string[] args)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            string cogSvcEndpoint = configuration["CognitiveServicesEndpoint"];
            string cogSvcKey = configuration["CognitiveServiceKey"];

            Console.Title = "Analyze image with Azure Computer Vision";

            cvClient = new ComputerVisionClient(new ApiKeyServiceClientCredentials(cogSvcKey))
            {
                Endpoint = cogSvcEndpoint
            };

            try
            {
                // Prompt the user for an image URL or file path
                Console.WriteLine("\nEnter a local image path or URL:");
                string input = Console.ReadLine();
              
                // If the user doesn't provide an image, use a default image
                if (string.IsNullOrEmpty(input))
                {
                    input = "images/street.jpg";
                }

                // Check if the input is a URL or a file path
                if (Uri.IsWellFormedUriString(input, UriKind.Absolute))
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        // Set the User-Agent request header to avoid HTTP 403 Forbidden error
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");

                        HttpResponseMessage response = await httpClient.GetAsync(input);
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                            using (Stream imageDataForThumbnail = new MemoryStream(imageBytes))
                            {
                                await GetThumbnailFromStream(imageDataForThumbnail);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Something is wrong with the URL you provided. Statuscode: {response.StatusCode}");
                            return;
                        }
                       
                    }
                    
                }
                else if (File.Exists(input))
                {
                    await AnalyzeImage(input);
                    await GetThumbnail(input);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid path or URL. Try again.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            try
            {
                string thumb = "C:\\Users\\Johanna\\source\\repos\\Labb2-ComputerVision\\Labb2-ComputerVision\\bin\\Debug\\net8.0\\thumbnailurl.jpg";
                if (File.Exists(thumb))
                {
                    await AnalyzeImage(thumb);
                    await GetThumbnail(thumb);
                }
                else
                {
                    Console.WriteLine("Have a nice day!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            } 
        }

        static async Task GetThumbnailFromStream(Stream imageData)
        {
            Console.WriteLine("\n Generating image from stream...");

            try
            {
                // Generate a thumbnail
                var thumbnailStream = await cvClient.GenerateThumbnailInStreamAsync(700, 700, imageData, false);

                // Save the thumbnail
                string thumbnailFileName = Path.Combine(Directory.GetCurrentDirectory(), "thumbnailurl.jpg");
                using (Stream thumbnailFile = File.Create(thumbnailFileName))
                {
                    await thumbnailStream.CopyToAsync(thumbnailFile);
                }

                Console.WriteLine($"Image saved as {thumbnailFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Something went wrong: {ex.Message}");
            }
        }
        
        static async Task AnalyzeImage(string imageFile)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n Analyzing image... \n");
            Console.ResetColor();
            Console.WriteLine("***************************************************************\n\n");
            
            // Specify features to be retrieved
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
            {
                    VisualFeatureTypes.Description,
                    VisualFeatureTypes.Tags,
                    VisualFeatureTypes.Categories,
                    VisualFeatureTypes.Brands,
                    VisualFeatureTypes.Objects,
                    VisualFeatureTypes.Adult
            };

            // Get image analysis
            using (var imageData = File.OpenRead(imageFile))
            {
                var analysis = await cvClient.AnalyzeImageInStreamAsync(imageData, features);

                foreach (var caption in analysis.Description.Captions)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\nDescription: {caption.Text} (Confidence: {caption.Confidence.ToString("P")})");
                    Console.ResetColor();
                }

                if (analysis.Tags.Count > 0)
                {
                    Console.WriteLine("\nTags:");
                    foreach (var tag in analysis.Tags)
                    {
                        Console.WriteLine($" -{tag.Name} (Confidence: {tag.Confidence.ToString("P")})");
                    }
                }

                List<LandmarksModel> landmarks = new List<LandmarksModel>();
                Console.WriteLine("\nCategories:");
                foreach (var category in analysis.Categories)  
                {
                    Console.WriteLine($" - {category.Name} (Confidence: {category.Score.ToString("P")})");

                    if (category.Detail?.Landmarks != null)
                    {
                        foreach (var landmark in category.Detail.Landmarks)  
                        {
                            if (!landmarks.Any(item => item.Name == landmark.Name))
                            {
                                landmarks.Add(landmark);
                            }
                        }
                    }
                }

                if (landmarks.Count > 0)
                {
                    Console.WriteLine("\nLandmarks:");
                    foreach (var landmark in landmarks) 
                    {
                        Console.WriteLine($" - {landmark.Name} (Confidence: {landmark.Confidence.ToString("P")})");
                    }
                }

                if (analysis.Brands.Count > 0)
                {
                    Console.WriteLine("\nBrands:");
                    foreach (var brand in analysis.Brands)  
                    {
                        Console.WriteLine($" - {brand.Name} (Confidence: {brand.Confidence.ToString("P")})");
                    }
                }

                if (analysis.Objects.Count > 0)
                {
                    Console.WriteLine("\nIdentified objects:");

                    Image image = Image.FromFile(imageFile);
                    Graphics graphics = Graphics.FromImage(image);
                    Pen pen = new Pen(Color.Cyan, 3);
                    Font font = new Font("Arial", 16);
                    SolidBrush brush = new SolidBrush(Color.Black);

                    foreach (var detectedObject in analysis.Objects)
                    {
                        // Print objects name, position and confidence
                        Console.WriteLine($" -{detectedObject.ObjectProperty} at position {detectedObject.Rectangle.X}, {detectedObject.Rectangle.Y}, width {detectedObject.Rectangle.W}, height {detectedObject.Rectangle.H} (Confidence: {detectedObject.Confidence.ToString("P")})");

                        // Draw object's bounding box
                        var r = detectedObject.Rectangle;
                        Rectangle rect = new Rectangle(r.X, r.Y, r.W, r.H);
                        graphics.DrawRectangle(pen, rect);
                        graphics.DrawString(detectedObject.ObjectProperty, font, brush, r.X, r.Y);
                    }

                    string projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;

                    string annotatedFolder = Path.Combine(projectDirectory, "AnnotatedImages");

                    // Create a folder for annotated images if it doesn't exist
                    if (!Directory.Exists(annotatedFolder))
                    {
                        Directory.CreateDirectory(annotatedFolder);
                    }

                    // Generate a unique ID for the annotated image
                    string iD = Guid.NewGuid().ToString();

                    // Save annotated image
                    String output_file = Path.Combine(annotatedFolder, $"objects{iD}.jpg");
                    image.Save(output_file);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n  Annotated image saved here:  " + output_file);
                    Console.ResetColor();

                    // Show the annotated image with the default image viewer
                    try
                    {
                        Process.Start(new ProcessStartInfo(output_file) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Cant show image: " + ex.Message);
                    }
                }

                string ratings = $"\nRatings:\n - Adult: {analysis.Adult.IsAdultContent}\n - Racy: {analysis.Adult.IsRacyContent}\n - Bloody: {analysis.Adult.IsGoryContent}";
                Console.WriteLine(ratings);

            }
        }

        static async Task GetThumbnail(string imageFile)
        {
            Console.WriteLine("\n\nEnter your prefered sizing for the thumbnail (ex. w:100, h:100)");

            int width;
            Console.Write("width: ");
            while (!int.TryParse(Console.ReadLine(), out width) || width > 1024 || width < 50)
            {
                Console.WriteLine("Please enter a number (min 50, max 1024): ");
            }
            int height;
            Console.Write("Height: ");
            while (!int.TryParse(Console.ReadLine(), out height) || height > 1024 || height < 50)
            {
                Console.WriteLine("Please enter a number (min 50, max 1024): ");
            }
          
            Console.WriteLine("\n Generating thumbnail..");

            string projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
            string thumbnailsFolder = Path.Combine(projectDirectory, "Thumbnails");

            if (!Directory.Exists(thumbnailsFolder))
            {
                Directory.CreateDirectory(thumbnailsFolder);
            }

            // Generate a unique ID for the thumbnail
            string Id = Guid.NewGuid().ToString();

            // Generate a thumbnail
            try
            {
                using (var imageData = File.OpenRead(imageFile))
                {
                    
                    var thumbnailStream = await cvClient.GenerateThumbnailInStreamAsync(width, height, imageData, true);

                    // Save the thumbnail
                    string thumbnailPath = Path.Combine(thumbnailsFolder,$"thumbnail{Id}.png");
                    using (Stream thumbnailFile = File.Create(thumbnailPath))
                    {
                        await thumbnailStream.CopyToAsync(thumbnailFile);  
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nThumbnail saved here:  {thumbnailPath}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Something went wrong: {ex.Message}");
            }
        }
    }
}
