# Discord Message Data Processor
This CLI tool takes the files downloaded from Discord and allows you to generate a heatmap from any direct messages, servers, or channels you choose. The heatmap shows the frequency of messages per day and supports multiple modes (by default the color scale is logarithmic)

<img width="906" height="650" alt="image" src="https://github.com/user-attachments/assets/658f5abf-0ce5-4bf0-9ad9-10e5dcd2d79d" />

## Functionality
- Multiple display modes

  <img width="546" height="124" alt="image" src="https://github.com/user-attachments/assets/a87a4414-0a29-4ce9-b97c-30f7a8847183" />

- Multiple ways to show months on the graph
  
  <img width="320" height="140" alt="image" src="https://github.com/user-attachments/assets/05ddebea-8fd4-4154-bb49-96dddfa86ce1" />

- A few color themes (Discord Blue, GitHub Green and White)
- Option to split the heatmap into multiple layers for complex image editing (this is not perfect due to colors not being in exact 0..1 range, so some slight post processing is needed)

## How to use
1. Request data from Discord (Settings -> Data & Privacy -> Request Data -> Select Messages)
2. Wait for the data to arrive
3. Unpack the program
4. Place all the data you received from Discord and put it next to the program's exe as `discord-package`
5. Run executable
