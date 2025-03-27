## About WebGL Templates

This directory is for WebGL platform. <br />
Copy these directories into `Assets/WebGLTemplates` to use MIDI callback functions.

- Default-MIDI
- Minimal-MIDI

## Difference from original templates

The modified file is `index.html` only.

Original:
```js
      script.onload = () => {
        createUnityInstance(canvas, config, (progress) => {
          progressBarFull.style.width = 100 * progress + "%";
        }).then((unityInstance) => {
```

Modified: add global `unityInstance` variable.
```js
      var unityInstance = null; // <- HERE
      script.onload = () => {
        createUnityInstance(canvas, config, (progress) => {
          progressBarFull.style.width = 100 * progress + "%";
        }).then((unityInst) => { // <- HERE
          unityInstance = unityInst; // <- HERE
```
