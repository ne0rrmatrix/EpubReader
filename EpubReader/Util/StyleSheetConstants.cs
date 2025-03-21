namespace EpubReader.Util;

public static class StyleSheetConstants
{
	public const string RadiumCSSDefaults = @"
		/*
		 * Readium CSS (v. 2.0.0-beta.6)
		 * Developers: Jiminy Panoz 
		 * Copyright (c) 2017. Readium Foundation. All rights reserved.
		 * Use of this source code is governed by a BSD-style license which is detailed in the
		 * LICENSE file present in the project repository where this source code is maintained.
		*/

		@namespace url(""http://www.w3.org/1999/xhtml"");

		@namespace epub url(""http://www.idpf.org/2007/ops"");

		@namespace m url(""http://www.w3.org/1998/Math/MathML"");

		@namespace svg url(""http://www.w3.org/2000/svg"");

		:root{
		  --RS__compFontFamily:var(--RS__baseFontFamily);
		  --RS__codeFontFamily:var(--RS__monospaceTf);

		  --RS__typeScale:1.125;
		  --RS__baseFontSize:100%;

		  --RS__flowSpacing:1.5rem;
		  --RS__paraSpacing:0;
		  --RS__paraIndent:1em;

		  --RS__linkColor:#0000EE;
		  --RS__visitedColor:#551A8B;
		}

		body{
		  font-size:var(--RS__baseFontSize);
		}

		h1, h2, h3, h4, h5, h6{
		  font-family:var(--RS__compFontFamily);
		}

		blockquote, figure, p, pre,
		aside, footer, form, hr{
		  margin-top:var(--RS__flowSpacing);
		  margin-bottom:var(--RS__flowSpacing);
		}

		p{
		  margin-top:var(--RS__paraSpacing);
		  margin-bottom:var(--RS__paraSpacing);
		  text-indent:var(--RS__paraIndent);
		}

		h1 + p, h2 + p, h3 + p, h4 + p, h5 + p, h6 + p,
		hr + p{
		  text-indent:0;
		}

		pre{
		  font-family:var(--RS__codeFontFamily);
		}

		code, kbd, samp, tt{
		  font-family:var(--RS__codeFontFamily);
		}

		sub, sup{
		  position:relative;
		  font-size:67.5%;
		  line-height:1;
		}

		sub{
		  bottom:-0.2ex;
		}

		sup{
		  bottom:0;
		}

		:link{
		  color:var(--RS__linkColor);
		}

		:visited{
		  color:var(--RS__visitedColor);
		}

		h1{
		  margin-top:calc(var(--RS__flowSpacing) * 2);
		  margin-bottom:calc(var(--RS__flowSpacing) * 2);
		  font-size:calc(((1em * var(--RS__typeScale)) * var(--RS__typeScale)) * var(--RS__typeScale));
		}

		h2{
		  margin-top:calc(var(--RS__flowSpacing) * 2);
		  margin-bottom:var(--RS__flowSpacing);
		  font-size:calc((1em * var(--RS__typeScale)) * var(--RS__typeScale));
		}

		h3{
		  margin-top:var(--RS__flowSpacing);
		  margin-bottom:var(--RS__flowSpacing);
		  font-size:calc(1em * var(--RS__typeScale));
		}

		h4{
		  margin-top:var(--RS__flowSpacing);
		  margin-bottom:var(--RS__flowSpacing);
		  font-size:1em;
		}

		h5{
		  margin-top:var(--RS__flowSpacing);
		  margin-bottom:var(--RS__flowSpacing);
		  font-size:1em;
		  font-variant:small-caps;
		}

		h6{
		  margin-top:var(--RS__flowSpacing);
		  margin-bottom:0;
		  font-size:1em;
		  text-transform:lowercase;
		  font-variant:small-caps;
		}

		dl, ol, ul{
		  margin-top:var(--RS__flowSpacing);
		  margin-bottom:var(--RS__flowSpacing);
		}

		table{
		  margin:var(--RS__flowSpacing) 0;
		  border:1px solid currentcolor;
		  border-collapse:collapse;
		  empty-cells:show;
		}

		thead, tbody, tfoot, table > tr{
		  vertical-align:top;
		}

		th{
		  text-align:left;
		}

		th, td{
		  padding:4px;
		  border:1px solid currentcolor;
		}";

	public const string RadiumCssConfig = @"
		/* Readium CSS 
		   Config module

		   A file allowing implementers to customize flags for reading modes,
		   user settings, etc.

		   Repo: https://github.com/readium/readium-css */

		/* Custom selectors
		   Syntax: @custom-selector :--variable selector
		   The selectors you will use for flags/switches
		   You can alternatively use classes or custom data-* attributes */

		/* User view = paged | scrolled */
		@custom-selector :--paged-view [style*=""readium-paged-on""];
		@custom-selector :--scroll-view [style*=""readium-scroll-on""];

		/* Font-family override */
		@custom-selector :--font-override [style*=""readium-font-on""];

		/* Advanced settings */
		@custom-selector :--advanced-settings [style*=""readium-advanced-on""];

		/* Reading Modes */
		@custom-selector :--sepia-mode [style*=""readium-sepia-on""];
		@custom-selector :--night-mode [style*=""readium-night-on""];

		/* Filters (images) */
		@custom-selector :--blend-filter [style*=""readium-blend-on""];
		@custom-selector :--darken-filter [style*=""readium-darken-on""];
		@custom-selector :--invert-filter [style*=""readium-invert-on""];
		@custom-selector :--invert-gaiji [style*=""readium-invertGaiji-on""];

		/* Disabling pagination for vertical writing */
		@custom-selector :--no-vertical-pagination [style*=""readium-noVerticalPagination-on""];

		/* Hiding ruby */
		@custom-selector :--no-ruby [style*=""readium-noRuby-on""];

		/* Accessibility normalization */
		@custom-selector :--a11y-normalize [style*=""readium-a11y-on""];

		/* Accessibility font. You can add selectors, using “, ” as a separator, if you have multiple fonts */
		@custom-selector :--a11y-font [style*=""AccessibleDfA""], [style*=""IA Writer Duospace""];

		/* Direction i.e. ltr and rtl */
		@custom-selector :--ltr [dir=""ltr""];
		@custom-selector :--rtl [dir=""rtl""];";

	public const string RadiumCssAfter = @"
		/*
			* Readium CSS (v. 2.0.0-beta.6)
			* Developers: Jiminy Panoz 
			* Copyright (c) 2017. Readium Foundation. All rights reserved.
			* Use of this source code is governed by a BSD-style license which is detailed in the
			* LICENSE file present in the project repository where this source code is maintained.
		*/

		@namespace url(""http://www.w3.org/1999/xhtml"");

		@namespace epub url(""http://www.idpf.org/2007/ops"");

		@namespace m url(""http://www.w3.org/1998/Math/MathML"");

		@namespace svg url(""http://www.w3.org/2000/svg"");

		:root{

			--RS__viewportWidth:100%;

			--RS__pageGutter:0;

			--RS__defaultLineLength:60rem;

			--RS__colGap:0;

			--RS__colCount:1;

			--RS__colWidth:100vw;
		}

		@page{
			margin:0 !important;
		}

		:root{
			position:relative;

			-webkit-column-width:var(--RS__colWidth);
			-moz-column-width:var(--RS__colWidth);
			column-width:var(--RS__colWidth);
			-webkit-column-count:var(--RS__colCount);
			-moz-column-count:var(--RS__colCount);
			column-count:var(--RS__colCount);

			-webkit-column-gap:var(--RS__colGap);
			-moz-column-gap:var(--RS__colGap);
			column-gap:var(--RS__colGap);
			-moz-column-fill:auto;
			column-fill:auto;
			width:var(--RS__viewportWidth);
			height:100vh;
			max-width:var(--RS__viewportWidth);
			max-height:100vh;
			min-width:var(--RS__viewportWidth);
			min-height:100vh;
			padding:0 !important;
			margin:0 !important;
			font-size:100% !important;
			-webkit-text-size-adjust:none;
			text-size-adjust:none;
			box-sizing:border-box;
			-webkit-touch-callout:none;
		}

		body{
			width:100%;
			max-width:var(--RS__defaultLineLength) !important;
			padding:0 var(--RS__pageGutter) !important;
			margin:0 auto !important;
			overflow:hidden;
			box-sizing:border-box;
		}

		@supports (overflow: clip){

			:root{
				overflow:clip;
			}

			body{
				overflow:clip;
				overflow-clip-margin:content-box;
			}
		}

		:root[style*=""readium-scroll-on""]{
			-webkit-columns:auto auto !important;
			-moz-columns:auto auto !important;
			columns:auto auto !important;
			width:auto !important;
			height:auto !important;
			max-width:none !important;
			max-height:none !important;
			min-width:0 !important;
			min-height:0 !important;
		}

		:root[style*=""readium-scroll-on""] body{
			max-width:var(--RS__defaultLineLength) !important;
			overflow:auto;
		}

		@supports (overflow: clip){

			:root[style*=""readium-scroll-on""]{
				overflow:auto;
			}

			:root[style*=""readium-scroll-on""] body{
				overflow:clip;
			}
		}

		:root[style*=""readium-night-on""]{

			--RS__selectionTextColor:inherit;

			--RS__selectionBackgroundColor:#b4d8fe;

			--RS__visitedColor:#0099E5;

			--RS__linkColor:#63caff;

			--RS__textColor:#FEFEFE;

			--RS__backgroundColor:#000000;
		}

		:root[style*=""readium-night-on""] *:not(a){
			color:inherit !important;
			background-color:transparent !important;
			border-color:currentcolor !important;
		}

		:root[style*=""readium-night-on""] svg text{
			fill:currentcolor !important;
			stroke:none !important;
		}

		:root[style*=""readium-night-on""] a:link,
		:root[style*=""readium-night-on""] a:link *{
			color:var(--RS__linkColor) !important;
		}

		:root[style*=""readium-night-on""] a:visited,
		:root[style*=""readium-night-on""] a:visited *{
			color:var(--RS__visitedColor) !important;
		}

		:root[style*=""readium-night-on""] img[class*=""gaiji""],
		:root[style*=""readium-night-on""] *[epub\:type~=""titlepage""] img:only-child,
		:root[style*=""readium-night-on""] *[epub|type~=""titlepage""] img:only-child{
			-webkit-filter:invert(100%);
			filter:invert(100%);
		}

		:root[style*=""readium-sepia-on""]{

			--RS__selectionTextColor:inherit;

			--RS__selectionBackgroundColor:#b4d8fe;

			--RS__visitedColor:#551A8B;

			--RS__linkColor:#0000EE;

			--RS__textColor:#121212;

			--RS__backgroundColor:#faf4e8;
		}

		:root[style*=""readium-sepia-on""] *:not(a){
			color:inherit !important;
			background-color:transparent !important;
		}

		:root[style*=""readium-sepia-on""] a:link,
		:root[style*=""readium-sepia-on""] a:link *{
			color:var(--RS__linkColor);
		}

		:root[style*=""readium-sepia-on""] a:visited,
		:root[style*=""readium-sepia-on""] a:visited *{
			color:var(--RS__visitedColor);
		}

		@media screen and (-ms-high-contrast: active){

			:root{
			color:windowText !important;
			background-color:window !important;
			}

			:root :not(#\#):not(#\#):not(#\#),
			:root :not(#\#):not(#\#):not(#\#) :not(#\#):not(#\#):not(#\#)
			:root :not(#\#):not(#\#):not(#\#) :not(#\#):not(#\#):not(#\#) :not(#\#):not(#\#):not(#\#){
			color:inherit !important;
			background-color:inherit !important;
			}

			.readiumCSS-mo-active-default{
			color:highlightText !important;
			background-color:highlight !important;
			}
		}

		@media screen and (-ms-high-contrast: white-on-black){

			:root[style*=""readium-night-on""] img[class*=""gaiji""],
			:root[style*=""readium-night-on""] *[epub\:type~=""titlepage""] img:only-child,
			:root[style*=""readium-night-on""] *[epub|type~=""titlepage""] img:only-child{
			-webkit-filter:none !important;
			filter:none !important;
			}

			:root[style*=""readium-night-on""][style*=""readium-invert-on""] img{
			-webkit-filter:none !important;
			filter:none !important;
			}

			:root[style*=""readium-night-on""][style*=""readium-darken-on""][style*=""readium-invert-on""] img{
			-webkit-filter:brightness(80%);
			filter:brightness(80%);
			}
		}

		@media screen and (inverted-colors){

			:root[style*=""readium-night-on""] img[class*=""gaiji""],
			:root[style*=""readium-night-on""] *[epub\:type~=""titlepage""] img:only-child,
			:root[style*=""readium-night-on""] *[epub|type~=""titlepage""] img:only-child{
			-webkit-filter:none !important;
			filter:none !important;
			}

			:root[style*=""readium-night-on""][style*=""readium-invert-on""] img{
			-webkit-filter:none !important;
			filter:none !important;
			}

			:root[style*=""readium-night-on""][style*=""readium-darken-on""][style*=""readium-invert-on""] img{
			-webkit-filter:brightness(80%);
			filter:brightness(80%);
			}
		}

		@media screen and (monochrome){
		}

		@media screen and (prefers-reduced-motion){
		}

		:root[style*=""--USER__backgroundColor""]{
			background-color:var(--USER__backgroundColor) !important;
		}

		:root[style*=""--USER__backgroundColor""] *{
			background-color:transparent !important;
		}

		:root[style*=""--USER__textColor""]{
			color:var(--USER__textColor) !important;
		}

		:root[style*=""--USER__textColor""] *:not(a){
			color:inherit !important;
			background-color:transparent !important;
			border-color:currentcolor !important;
		}

		:root[style*=""--USER__textColor""] svg text{
			fill:currentcolor !important;
			stroke:none !important;
		}

		:root[style*=""--USER__linkColor""] a:link,
		:root[style*=""--USER__linkColor""] a:link *{
			color:var(--USER__linkColor) !important;
		}

		:root[style*=""--USER__visitedColor""] a:visited,
		:root[style*=""--USER__visitedColor""] a:visited *{
			color:var(--USER__visitedColor) !important;
		}

		:root[style*=""--USER__selectionBackgroundColor""][style*=""--USER__selectionTextColor""] ::-moz-selection{
			color:var(--USER__selectionTextColor) !important;
			background-color:var(--USER__selectionBackgroundColor) !important;
		}

		:root[style*=""--USER__selectionBackgroundColor""][style*=""--USER__selectionTextColor""] ::selection{
			color:var(--USER__selectionTextColor) !important;
			background-color:var(--USER__selectionBackgroundColor) !important;
		}

		:root[style*=""--USER__colCount""]{
			-webkit-column-count:var(--USER__colCount);
			-moz-column-count:var(--USER__colCount);
			column-count:var(--USER__colCount);

			--RS__colWidth:auto;
		}

		:root[style*=""--USER__colCount: 0""],
		:root[style*=""--USER__colCount:0""]{
			-webkit-column-count:1;
			-moz-column-count:1;
			column-count:1;
		}

		:root[style*=""--USER__colCount: 0""],
		:root[style*=""--USER__colCount:0""],
		:root[style*=""--USER__colCount: 1""],
		:root[style*=""--USER__colCount:1""]{
			--RS__colWidth:100vw;
		}

		:root[style*=""--USER__lineLength""] body{
			max-width:var(--USER__lineLength) !important;
			}

		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign""]{
			text-align:var(--USER__textAlign);
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign""] *:not(blockquote):not(figcaption) p,
		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign""] li{
			text-align:var(--USER__textAlign) !important;
			-moz-text-align-last:auto !important;
			-epub-text-align-last:auto !important;
			text-align-last:auto !important;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign: justify""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign:justify""] body{
			-webkit-hyphens:auto;
			-moz-hyphens:auto;
			-ms-hyphens:auto;
			-epub-hyphens:auto;
			hyphens:auto;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign: left""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign:left""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign: right""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__textAlign:right""] body{
			-webkit-hyphens:none;
			-moz-hyphens:none;
			-ms-hyphens:none;
			-epub-hyphens:none;
			hyphens:none;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__bodyHyphens""]{
			-webkit-hyphens:var(--USER__bodyHyphens) !important;
			-moz-hyphens:var(--USER__bodyHyphens) !important;
			-ms-hyphens:var(--USER__bodyHyphens) !important;
			-epub-hyphens:var(--USER__bodyHyphens) !important;
			hyphens:var(--USER__bodyHyphens) !important;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__bodyHyphens""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__bodyHyphens""] p,
		:root[style*=""readium-advanced-on""][style*=""--USER__bodyHyphens""] li,
		:root[style*=""readium-advanced-on""][style*=""--USER__bodyHyphens""] div,
		:root[style*=""readium-advanced-on""][style*=""--USER__bodyHyphens""] dd{
			-webkit-hyphens:inherit;
			-moz-hyphens:inherit;
			-ms-hyphens:inherit;
			-epub-hyphens:inherit;
			hyphens:inherit;
		}

		:root[style*=""readium-font-on""][style*=""--USER__fontFamily""]{
			font-family:var(--USER__fontFamily) !important;
		}

		:root[style*=""readium-font-on""][style*=""--USER__fontFamily""] *:not(code):not(var):not(kbd):not(samp){
			font-family:inherit !important;
		}

		:root[style*=""readium-font-on""][style*=""AccessibleDfA""]{
			font-family:AccessibleDfA, Verdana, Tahoma, ""Trebuchet MS"", sans-serif !important;
			--RS__lineHeightCompensation:1.167;
		}

		:root[style*=""readium-font-on""][style*=""IA Writer Duospace""]{
			font-family:""IA Writer Duospace"", Menlo, ""DejaVu Sans Mono"", ""Bitstream Vera Sans Mono"", Courier, monospace !important;
			--RS__lineHeightCompensation:1.167;
		}

		:root[style*=""readium-font-on""][style*=""readium-a11y-on""]{
			font-family:var(--USER__fontFamily) !important;
			--RS__lineHeightCompensation:1.167;
		}

		:root[style*=""readium-font-on""][style*=""AccessibleDfA""],:root[style*=""readium-font-on""][style*=""IA Writer Duospace""],
		:root[style*=""readium-font-on""][style*=""readium-a11y-on""]{
			font-style:normal !important;
			font-weight:normal !important;
		}

		:root[style*=""readium-font-on""][style*=""AccessibleDfA""] *:not(code):not(var):not(kbd):not(samp),:root[style*=""readium-font-on""][style*=""IA Writer Duospace""] *:not(code):not(var):not(kbd):not(samp),
		:root[style*=""readium-font-on""][style*=""readium-a11y-on""] *:not(code):not(var):not(kbd):not(samp){
			font-family:inherit !important;
			font-style:inherit !important;
			font-weight:inherit !important;
		}

		:root[style*=""readium-font-on""][style*=""AccessibleDfA""] *,:root[style*=""readium-font-on""][style*=""IA Writer Duospace""] *,
		:root[style*=""readium-font-on""][style*=""readium-a11y-on""] *{
			text-decoration:none !important;
			font-variant-caps:normal !important;
			font-variant-numeric:normal !important;
			font-variant-position:normal !important;
		}

		:root[style*=""readium-font-on""][style*=""AccessibleDfA""] sup,:root[style*=""readium-font-on""][style*=""IA Writer Duospace""] sup,
		:root[style*=""readium-font-on""][style*=""readium-a11y-on""] sup,
		:root[style*=""readium-font-on""][style*=""AccessibleDfA""] sub,
		:root[style*=""readium-font-on""][style*=""IA Writer Duospace""] sub,
		:root[style*=""readium-font-on""][style*=""readium-a11y-on""] sub{
			font-size:1rem !important;
			vertical-align:baseline !important;
		}

		:root[style*=""--USER__fontSize""] body{
			zoom:var(--USER__fontSize) !important;
		}

		@supports not (zoom: 1){

			:root[style*=""--USER__fontSize""]{
			font-size:var(--USER__fontSize) !important;
			}
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__lineHeight""]{
			line-height:var(--USER__lineHeight) !important;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__lineHeight""] body,
		:root[style*=""readium-advanced-on""][style*=""--USER__lineHeight""] p,
		:root[style*=""readium-advanced-on""][style*=""--USER__lineHeight""] li,
		:root[style*=""readium-advanced-on""][style*=""--USER__lineHeight""] div{
			line-height:inherit;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__paraSpacing""] p{
			margin-top:var(--USER__paraSpacing) !important;
			margin-bottom:var(--USER__paraSpacing) !important;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__paraIndent""] p{
			text-indent:var(--USER__paraIndent) !important;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__paraIndent""] p *,
		:root[style*=""readium-advanced-on""][style*=""--USER__paraIndent""] p:first-letter{
			text-indent:0 !important;
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] h1,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] h2,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] h3,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] h4,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] h5,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] h6,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] p,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] li,
		:root[style*=""readium-advanced-on""][style*=""--USER__wordSpacing""] div{
			word-spacing:var(--USER__wordSpacing);
		}

		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] h1,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] h2,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] h3,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] h4,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] h5,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] h6,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] p,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] li,
		:root[style*=""readium-advanced-on""][style*=""--USER__letterSpacing""] div{
			letter-spacing:var(--USER__letterSpacing);
			font-variant:none;
		}

		:root[style*=""readium-font-on""][style*=""--USER__fontWeight""] body{
			font-weight:var(--USER__fontWeight) !important;
		}

		:root[style*=""readium-font-on""][style*=""--USER__fontWeight""] b,
		:root[style*=""readium-font-on""][style*=""--USER__fontWeight""] strong{
			font-weight:bolder;
		}

		:root[style*=""readium-font-on""][style*=""--USER__fontWidth""] body{
			font-stretch:var(--USER__fontWidth) !important;
		}

		:root[style*=""readium-font-on""][style*=""--USER__fontOpticalSizing""] body{
			font-optical-sizing:var(--USER__fontOpticalSizing) !important;
		}

		:root[style*=""readium-blend-on""] svg,
		:root[style*=""readium-blend-on""] img{
			background-color:transparent !important;
			mix-blend-mode:multiply !important;
		}

		:root[style*=""--USER__darkenImages""] img{
			-webkit-filter:brightness(var(--USER__darkenImages)) !important;
			filter:brightness(var(--USER__darkenImages)) !important;
		}

		:root[style*=""readium-darken-on""] img{
			-webkit-filter:brightness(80%) !important;
			filter:brightness(80%) !important;
		}

		:root[style*=""--USER__invertImages""] img{
			-webkit-filter:invert(var(--USER__invertImages)) !important;
			filter:invert(var(--USER__invertImages)) !important;
		}

		:root[style*=""readium-invert-on""] img{
			-webkit-filter:invert(100%) !important;
			filter:invert(100%) !important;
		}

		:root[style*=""--USER__darkenImages""][style*=""--USER__invertImages""] img{
			-webkit-filter:brightness(var(--USER__darkenImages)) invert(var(--USER__invertImages)) !important;
			filter:brightness(var(--USER__darkenImages)) invert(var(--USER__invertImages)) !important;
		}

		:root[style*=""readium-darken-on""][style*=""--USER__invertImages""] img{
			-webkit-filter:brightness(80%) invert(var(--USER__invertImages)) !important;
			filter:brightness(80%) invert(var(--USER__invertImages)) !important;
		}

		:root[style*=""--USER__darkenImages""][style*=""readium-invert-on""] img{
			-webkit-filter:brightness(var(--USER__darkenImages)) invert(100%) !important;
			filter:brightness(var(--USER__darkenImages)) invert(100%) !important;
		}

		:root[style*=""readium-darken-on""][style*=""readium-invert-on""] img{
			-webkit-filter:brightness(80%) invert(100%) !important;
			filter:brightness(80%) invert(100%) !important;
		}

		:root[style*=""--USER__invertGaiji""] img[class*=""gaiji""]{
			-webkit-filter:invert(var(--USER__invertGaiji)) !important;
			filter:invert(var(--USER__invertGaiji)) !important;
		}

		:root[style*=""readium-invertGaiji-on""] img[class*=""gaiji""]{
			-webkit-filter:invert(100%) !important;
			filter:invert(100%) !important;
		}

		@supports not (zoom: 1){

			:root[style*=""readium-advanced-on""]{
			--USER__typeScale:1.2;
			}

			:root[style*=""readium-advanced-on""] p,
			:root[style*=""readium-advanced-on""] li,
			:root[style*=""readium-advanced-on""] div,
			:root[style*=""readium-advanced-on""] pre,
			:root[style*=""readium-advanced-on""] dd{
			font-size:1rem !important;
			}

			:root[style*=""readium-advanced-on""] h1{
			font-size:1.75rem !important;
			font-size:calc(((1rem * var(--USER__typeScale)) * var(--USER__typeScale)) * var(--USER__typeScale)) !important;
			}

			:root[style*=""readium-advanced-on""] h2{
			font-size:1.5rem !important;
			font-size:calc((1rem * var(--USER__typeScale)) * var(--USER__typeScale)) !important;
			}

			:root[style*=""readium-advanced-on""] h3{
			font-size:1.25rem !important;
			font-size:calc(1rem * var(--USER__typeScale)) !important;
			}

			:root[style*=""readium-advanced-on""] h4,
			:root[style*=""readium-advanced-on""] h5,
			:root[style*=""readium-advanced-on""] h6{
			font-size:1rem !important;
			}

			:root[style*=""readium-advanced-on""] small{
			font-size:smaller !important;
			}

			:root[style*=""readium-advanced-on""] sub,
			:root[style*=""readium-advanced-on""] sup{
			font-size:67.5% !important;
			}

			:root[style*=""readium-advanced-on""][style*=""--USER__typeScale""] h1{
			font-size:calc(((1rem * var(--USER__typeScale)) * var(--USER__typeScale)) * var(--USER__typeScale)) !important;
			}

			:root[style*=""readium-advanced-on""][style*=""--USER__typeScale""] h2{
			font-size:calc((1rem * var(--USER__typeScale)) * var(--USER__typeScale)) !important;
			}

			:root[style*=""readium-advanced-on""][style*=""--USER__typeScale""] h3{
			font-size:calc(1rem * var(--USER__typeScale)) !important;
			}
		}";

	public const string RadiumCssBefore = @"
		/*
		 * Readium CSS (v. 2.0.0-beta.6)
		 * Developers: Jiminy Panoz 
		 * Copyright (c) 2017. Readium Foundation. All rights reserved.
		 * Use of this source code is governed by a BSD-style license which is detailed in the
		 * LICENSE file present in the project repository where this source code is maintained.
		*/

		@namespace url(""http://www.w3.org/1999/xhtml"");

		@namespace epub url(""http://www.idpf.org/2007/ops"");

		@namespace m url(""http://www.w3.org/1998/Math/MathML"");

		@namespace svg url(""http://www.w3.org/2000/svg"");

		@-ms-viewport{
		  width:device-width;
		}

		@viewport{
		  width:device-width;
		  zoom:1;
		}

		:root{

		  --RS__monospaceTf:ui-monospace, 'Andale Mono', 'Cascadia Code', 'Source Code Pro', Menlo, Consolas, 'DejaVu Sans Mono', monospace;

		  --RS__humanistTf:Seravek, Calibri, 'Gill Sans Nova', Roboto, Ubuntu, 'DejaVu Sans', source-sans-pro, sans-serif;

		  --RS__sansTf:-ui-sans-serif, -apple-system, system-ui, BlinkMacSystemFont, 'Segoe UI Variable', 'Segoe UI', Inter, Roboto, 'Helvetica Neue', 'Arial Nova', 'Liberation Sans', Arial, sans-serif;

		  --RS__modernTf:Athelas, Constantia, Charter, 'Bitstream Charter', Cambria, 'Georgia Pro', Georgia, serif;

		  --RS__oldStyleTf:'Iowan Old Style', Sitka, 'Sitka Text', Palatino, 'Book Antiqua', 'URW Palladio L', P052, serif;
		  --RS__baseFontFamily:var(--RS__oldStyleTf);
		  --RS__lineHeightCompensation:1;

		  --RS__baseLineHeight:calc(1.5 * var(--RS__lineHeightCompensation));
		}

		html{
		  font-family:var(--RS__baseFontFamily);
		  line-height:1.6;
		  line-height:var(--RS__baseLineHeight);
		  text-rendering:optimizelegibility;
		}

		h1, h2, h3{
		  line-height:normal;
		}

		:lang(ja),
		:lang(zh),
		:lang(ko){
		  word-wrap:break-word;
		  -webkit-line-break:strict;
		  -epub-line-break:strict;
		  line-break:strict;
		}

		math{
		  font-family:""Latin Modern Math"", ""STIX Two Math"", ""XITS Math"", ""STIX Math"", ""Libertinus Math"", ""TeX Gyre Termes Math"", ""TeX Gyre Bonum Math"", ""TeX Gyre Schola"", ""DejaVu Math TeX Gyre"", ""TeX Gyre Pagella Math"", ""Asana Math"", ""Cambria Math"", ""Lucida Bright Math"", ""Minion Math"", STIXGeneral, STIXSizeOneSym, Symbol, ""Times New Roman"", serif;
		}

		:lang(am){
		  --RS__baseFontFamily:kefa, nyala, roboto, noto, ""Noto Sans Ethiopic"", serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(ar){
		  --RS__baseFontFamily:""Geeza Pro"", ""Arabic Typesetting"", roboto, noto, ""Noto Naskh Arabic"", ""Times New Roman"", serif;
		}

		:lang(bn){
		  --RS__baseFontFamily:""Kohinoor Bangla"", ""Bangla Sangam MN"", vrinda, roboto, noto, ""Noto Sans Bengali"", sans-serif;
		  --RS__lineHeightCompensation:1.067;
		}

		:lang(bo){
		  --RS__baseFontFamily:kailasa, ""Microsoft Himalaya"", roboto, noto, ""Noto Sans Tibetan"", sans-serif;
		}

		:lang(chr){
		  --RS__baseFontFamily:""Plantagenet Cherokee"", roboto, noto, ""Noto Sans Cherokee"";
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(fa){
		  --RS__baseFontFamily:""Geeza Pro"", ""Arabic Typesetting"", roboto, noto, ""Noto Naskh Arabic"", ""Times New Roman"", serif;
		}

		:lang(gu){
		  --RS__baseFontFamily:""Gujarati Sangam MN"", ""Nirmala UI"", shruti, roboto, noto, ""Noto Sans Gujarati"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(he){
		  --RS__baseFontFamily:""New Peninim MT"", ""Arial Hebrew"", gisha, ""Times New Roman"", roboto, noto, ""Noto Sans Hebrew"" sans-serif;
		  --RS__lineHeightCompensation:1.1;
		}

		:lang(hi){
		  --RS__baseFontFamily:""Kohinoor Devanagari"", ""Devanagari Sangam MN"", kokila, ""Nirmala UI"", roboto, noto, ""Noto Sans Devanagari"", sans-serif;

		  --RS__lineHeightCompensation:1.1;
		}

		:lang(hy){
		  --RS__baseFontFamily:mshtakan, sylfaen, roboto, noto, ""Noto Serif Armenian"", serif;
		}

		:lang(iu){
		  --RS__baseFontFamily:""Euphemia UCAS"", euphemia, roboto, noto, ""Noto Sans Canadian Aboriginal"", sans-serif;
		}

		:lang(ja){
		  --RS__baseFontFamily:yugothic, ""Hiragino Maru Gothic ProN"", ""Hiragino Sans"", ""Yu Gothic UI"", ""Meiryo UI"", ""MS Gothic"", roboto, noto, ""Noto Sans CJK JP"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		  --RS__serif-ja:""Hiragino Mincho ProN"", ""Hiragino Mincho Pro"", ""YuMincho"", ""BIZ UDPMincho"", ""Yu Mincho"", ""ＭＳ Ｐ明朝"", ""MS PMincho"", serif;
		  --RS__sans-serif-ja:""Hiragino Sans"", ""Hiragino Kaku Gothic ProN"", ""Hiragino Kaku Gothic Pro"", ""ヒラギノ角ゴ W3"", ""YuGothic"", ""Yu Gothic Medium"", ""BIZ UDPGothic"", ""Yu Gothic"", ""ＭＳ Ｐゴシック"", ""MS PGothic"", sans-serif;
		  --RS__serif-ja-v:""Hiragino Mincho ProN"", ""Hiragino Mincho Pro"", ""YuMincho"", ""BIZ UDMincho"", ""Yu Mincho"", ""ＭＳ明朝"", ""MS Mincho"", serif;
		  --RS__sans-serif-ja-v:""Hiragino Sans"", ""Hiragino Kaku Gothic ProN"", ""Hiragino Kaku Gothic Pro"", ""ヒラギノ角ゴ W3"", ""YuGothic"", ""Yu Gothic Medium"", ""BIZ UDGothic"", ""Yu Gothic"", ""ＭＳゴシック"", ""MS Gothic"", sans-serif;
		}

		:lang(km){
		  --RS__baseFontFamily:""Khmer Sangam MN"", ""Leelawadee UI"", ""Khmer UI"", roboto, noto, ""Noto Sans Khmer"", sans-serif;
		  --RS__lineHeightCompensation:1.067;
		}

		:lang(kn){
		  --RS__baseFontFamily:""Kannada Sangam MN"", ""Nirmala UI"", tunga, roboto, noto, ""Noto Sans Kannada"", sans-serif;
		  --RS__lineHeightCompensation:1.1;
		}

		:lang(ko){
		  --RS__baseFontFamily:""Nanum Gothic"", ""Apple SD Gothic Neo"", ""Malgun Gothic"", roboto, noto, ""Noto Sans CJK KR"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(lo){
		  --RS__baseFontFamily:""Lao Sangam MN"", ""Leelawadee UI"", ""Lao UI"", roboto, noto, ""Noto Sans Lao"", sans-serif;
		}

		:lang(ml){
		  --RS__baseFontFamily:""Malayalam Sangam MN"", ""Nirmala UI"", kartika, roboto, noto, ""Noto Sans Malayalam"", sans-serif;
		  --RS__lineHeightCompensation:1.067;
		}

		:lang(or){
		  --RS__baseFontFamily:""Oriya Sangam MN"", ""Nirmala UI"", kalinga, roboto, noto, ""Noto Sans Oriya"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(pa){
		  --RS__baseFontFamily:""Gurmukhi MN"", ""Nirmala UI"", kartika, roboto, noto, ""Noto Sans Gurmukhi"", sans-serif;
		  --RS__lineHeightCompensation:1.1;
		}

		:lang(si){
		  --RS__baseFontFamily:""Sinhala Sangam MN"", ""Nirmala UI"", ""Iskoola Pota"", roboto, noto, ""Noto Sans Sinhala"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(ta){
		  --RS__baseFontFamily:""Tamil Sangam MN"", ""Nirmala UI"", latha, roboto, noto, ""Noto Sans Tamil"", sans-serif;
		  --RS__lineHeightCompensation:1.067;
		}

		:lang(te){
		  --RS__baseFontFamily:""Kohinoor Telugu"", ""Telugu Sangam MN"", ""Nirmala UI"", gautami, roboto, noto, ""Noto Sans Telugu"", sans-serif;
		}

		:lang(th){
		  --RS__baseFontFamily:""Thonburi"", ""Leelawadee UI"", ""Cordia New"", roboto, noto, ""Noto Sans Thai"", sans-serif;
		  --RS__lineHeightCompensation:1.067;
		}

		:lang(zh){
		  --RS__baseFontFamily:""方体"", ""PingFang SC"", ""黑体"", ""Heiti SC"", ""Microsoft JhengHei UI"", ""Microsoft JhengHei"", roboto, noto, ""Noto Sans CJK SC"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(zh-Hant),
		:lang(zh-TW){
		  --RS__baseFontFamily:""方體"", ""PingFang TC"", ""黑體"", ""Heiti TC"", ""Microsoft JhengHei UI"", ""Microsoft JhengHei"", roboto, noto, ""Noto Sans CJK TC"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:lang(zh-HK){
		  --RS__baseFontFamily:""方體"", ""PingFang HK"", ""方體"", ""PingFang TC"", ""黑體"", ""Heiti TC"", ""Microsoft JhengHei UI"", ""Microsoft JhengHei"", roboto, noto, ""Noto Sans CJK TC"", sans-serif;
		  --RS__lineHeightCompensation:1.167;
		}

		:root{

		  --RS__selectionTextColor:inherit;

		  --RS__selectionBackgroundColor:#b4d8fe;

		  --RS__visitedColor:#551A8B;

		  --RS__linkColor:#0000EE;

		  --RS__textColor:#121212;

		  --RS__backgroundColor:#FFFFFF;
		}

		:root{
		  color:var(--RS__textColor) !important;
		  background-color:var(--RS__backgroundColor) !important;
		}

		::-moz-selection{
		  color:var(--RS__selectionTextColor);
		  background-color:var(--RS__selectionBackgroundColor);
		}

		::selection{
		  color:var(--RS__selectionTextColor);
		  background-color:var(--RS__selectionBackgroundColor);
		}

		@font-face{
		  font-family:AccessibleDfA;
		  font-style:normal;
		  font-weight:normal;
		  src:local(""AccessibleDfA""), url(""fonts/AccessibleDfA-Regular.woff2"") format(""woff2""), url(""fonts/AccessibleDfA-Regular.woff"") format(""woff"");
		}

		@font-face{
		  font-family:AccessibleDfA;
		  font-style:normal;
		  font-weight:bold;
		  src:local(""AccessibleDfA""), url(""fonts/AccessibleDfA-Bold.woff2"") format(""woff2"");
		}

		@font-face{
		  font-family:AccessibleDfA;
		  font-style:italic;
		  font-weight:normal;
		  src:local(""AccessibleDfA""), url(""fonts/AccessibleDfA-Italic.woff2"") format(""woff2"");
		}

		@font-face{
		  font-family:""IA Writer Duospace"";
		  font-style:normal;
		  font-weight:normal;
		  src:local(""iAWriterDuospace-Regular""), url(""fonts/iAWriterDuospace-Regular.ttf"") format(""truetype"");
		}

		body{
		  widows:2;
		  orphans:2;
		}

		figcaption, th, td{
		  widows:1;
		  orphans:1;
		}

		h2, h3, h4, h5, h6, dt,
		hr, caption{
		  -webkit-column-break-after:avoid;
		  page-break-after:avoid;
		  break-after:avoid;
		}

		h1, h2, h3, h4, h5, h6, dt,
		figure, tr{
		  -webkit-column-break-inside:avoid;
		  page-break-inside:avoid;
		  break-inside:avoid;
		}

		body{
		  -webkit-hyphenate-character:""\002D"";
		  -moz-hyphenate-character:""\002D"";
		  -ms-hyphenate-character:""\002D"";
		  hyphenate-character:""\002D"";
		  -webkit-hyphenate-limit-lines:3;
		  -ms-hyphenate-limit-lines:3;
		  hyphenate-limit-lines:3;
		}

		h1, h2, h3, h4, h5, h6, dt,
		figcaption, pre, caption, address,
		center, code, var{
		  -ms-hyphens:none;
		  -moz-hyphens:none;
		  -webkit-hyphens:none;
		  -epub-hyphens:none;
		  hyphens:none;
		}

		body{
		  font-variant-numeric:oldstyle-nums proportional-nums;
		}

		:lang(ja) body,
		:lang(zh) body,
		:lang(ko) body{
		  font-variant-numeric:lining-nums proportional-nums;
		}

		h1, h2, h3, h4, h5, h6, dt{
		  font-variant-numeric:lining-nums proportional-nums;
		}

		table{
		  font-variant-numeric:lining-nums tabular-nums;
		}

		code, var{
		  font-variant-ligatures:none;
		  font-variant-numeric:lining-nums tabular-nums slashed-zero;
		}

		rt{
		  font-variant-east-asian:ruby;
		}

		:lang(ar){
		  font-variant-ligatures:common-ligatures;
		}

		:lang(ko){
		  font-kerning:normal;
		}

		hr{
		  color:inherit;
		  border-color:currentcolor;
		}

		table, th, td{
		  border-color:currentcolor;
		}

		figure, blockquote{
		  margin:1em 5%;
		}

		ul, ol{
		  padding-left:5%;
		}

		dd{
		  margin-left:5%;
		}

		pre{
		  white-space:pre-wrap;
		  -ms-tab-size:2;
		  -moz-tab-size:2;
		  -webkit-tab-size:2;
		  tab-size:2;
		}

		abbr[title], acronym[title]{
		  text-decoration:dotted underline;
		}

		nobr wbr{
		  white-space:normal;
		}

		ruby > rt, ruby > rp{
		  -webkit-user-select:none;
		  -moz-user-select:none;
		  -ms-user-select:none;
		  user-select:none;
		}

		*:lang(ja):not(:lang(ja-Latn)):not(:lang(ja-Cyrl)),
		*:lang(zh):not(:lang(zh-Latn)):not(:lang(zh-Cyrl)),
		*:lang(ko):not(:lang(ko-Latn)):not(:lang(ko-Cyrl)),
		:lang(ja):not(:lang(ja-Latn)):not(:lang(ja-Cyrl)) cite, 
		:lang(ja):not(:lang(ja-Latn)):not(:lang(ja-Cyrl)) dfn, 
		:lang(ja):not(:lang(ja-Latn)):not(:lang(ja-Cyrl)) em, 
		:lang(ja):not(:lang(ja-Latn)):not(:lang(ja-Cyrl)) i,
		:lang(zh):not(:lang(zh-Latn)):not(:lang(zh-Cyrl)) cite, 
		:lang(zh):not(:lang(zh-Latn)):not(:lang(zh-Cyrl)) dfn, 
		:lang(zh):not(:lang(zh-Latn)):not(:lang(zh-Cyrl)) em, 
		:lang(zh):not(:lang(zh-Latn)):not(:lang(zh-Cyrl)) i,
		:lang(ko):not(:lang(ko-Latn)):not(:lang(ko-Cyrl)) cite, 
		:lang(ko):not(:lang(ko-Latn)):not(:lang(ko-Cyrl)) dfn, 
		:lang(ko):not(:lang(ko-Latn)):not(:lang(ko-Cyrl)) em, 
		:lang(ko):not(:lang(ko-Latn)):not(:lang(ko-Cyrl)) i{
		  font-style:normal;
		}

		:lang(ja) a,
		:lang(zh) a,
		:lang(ko) a{
		  text-decoration:none;
		}

		:root{
		  --RS__maxMediaWidth:100%;
		  --RS__maxMediaHeight:100vh;
		  --RS__boxSizingMedia:border-box;
		  --RS__boxSizingTable:border-box;
		}

		a, a span, span a, h1, h2, h3, h4, h5, h6{
		  word-wrap:break-word;
		}

		div{
		  max-width:var(--RS__maxMediaWidth);
		}

		img, svg|svg, video{
		  object-fit:contain;

		  width:auto;
		  height:auto;
		  max-width:var(--RS__maxMediaWidth);
		  max-height:var(--RS__maxMediaHeight) !important;
		  box-sizing:var(--RS__boxSizingMedia);
		  -webkit-column-break-inside:avoid;
		  page-break-inside:avoid;
		  break-inside:avoid;
		}

		audio{
			max-width:100%;
			-webkit-column-break-inside:avoid;
			page-break-inside:avoid;
			break-inside:avoid;
		  }

		table{
		  max-width:var(--RS__maxMediaWidth);
		  box-sizing:var(--RS__boxSizingTable);
		}";

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
