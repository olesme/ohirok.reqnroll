@attachments
Feature: Every kind of attachment
  One scenario per way a test can attach something, to see how the report shows each. Reqnroll's own API attaches
  files (output.AddAttachment) and text (output.WriteLine); the Ohirok embeds add inline images, image URLs, HTML
  and callouts. Search for "attachment" in the report to get here quickly.

  Scenario: Reqnroll attachments - files and text
    When a line of text is written to the output
    And a PNG file is attached the Reqnroll way
    And a text file is attached the Reqnroll way
    And a JSON file is attached the Reqnroll way
    And an HTML file is attached the Reqnroll way

  Scenario: Ohirok embeds - images
    When an image is embedded inline
    And an image is embedded inline, expanded
    And an image is embedded by URL

  Scenario: Ohirok embeds - HTML and callouts
    When HTML with a table and links is embedded
    And an info, a warning and an error callout are embedded

  @attachment-in-hook
  Scenario: Attachments written by hooks
    When a line of text is written to the output
    And an image is embedded inline
