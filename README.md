# HttpFolderDownloader
A simple command line to navigate and download content from a Http representation of a system folder

## Description

This app will look for any `href` reference in the feed and will try to navigate over them and download when it's downloadable. 

## Usage
`HttpFolderDownloader <URL> <PATH_TO_SAVE_FILES> [Depth] [--downloadContent <contentType>] [--navigateContent <contentType>] [--overwrite <true|false>]`
* For multiple values, separate them by comma

`--downloadType`              MIME types for downloading

`--navigateContent`           MIME types for navigation

When those variables are defined, the app will try to either download or navigate depending on the configuration by `Content-Types`.

`Depth` 
* not defined or less than or equal 0, for infinite `depth` of links. 
* If it is defined, 1 is the current level, 2 for the current level and 1 more and so on.

`Overwrite` variable defines if files on download path should be updated or not.
