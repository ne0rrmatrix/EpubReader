namespace EpubReader.Util;

public static class StyleSheetConstants
{
	/// <summary>
	/// Sets the style for the scroll container.
	/// </summary>
	/// <param name="columns"></param>
	/// <returns></returns>
	public static string GetStyle(int columns)
	{
		return $@"
    ::-webkit-scrollbar {{
        display: none;
    }}

    * {{
        -webkit-touch-callout: none;
    }}
	
    #scrollContainer {{
        columns: {columns};
        overflow-x: auto;
		margin-top: 1em;
        height: 95vh;
		
    }}

    #scrollContainer p, h1, h2, h3, h4 {{
        text-align: justify;
		margin-left: 1em;
        margin-right: 1em;
    }}";
	}

	/// <summary>
	/// Sets the style for the scroll container.
	/// </summary>
	public static readonly string ImageStyle = @"
		.image_full {
		text-align: center;
		}
    
		.image_full img {
		  display: block;
		  margin: 0 auto;
		  max-width: 100%;
		  height: 100vh;
		}
    
		/* New CSS for cover_image */
		.cover_image {
		  text-align: center;
		}
    
		.cover_image img {
		  display: block;
		  margin: 0 auto;
		  max-width: 100%;
		  height: 100vh;
		}

		/* Optional: if you need to set the image to inline-block */
		.cover-image img {
		  display: inline-block;
		}
		img {
		  max-width: 100vw; /* Ensures the image doesn't exceed the page width */
		  height: 100vh; /* Maintains aspect ratio by scaling height proportionally */
		  display: block; /* Removes extra space below inline images */
		}";
}
