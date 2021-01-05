var recorderNode = null;
var audioContext = null;
var audioInput = null;
var microphone_stream = null;

// Convert an array buffer to its binary form
// of a base64 string.
// https://stackoverflow.com/questions/9267899/arraybuffer-to-base64-encoded-string
function arrayBufferToBase64(buffer, callback)
{
	var blob = new Blob([buffer], { type: 'application/octet-binary' });

	var reader = new FileReader();

	reader.onload = function (evt)
	{
		var dataurl = evt.target.result;
		callback(dataurl.substr(dataurl.indexOf(',') + 1));
	};
	reader.readAsDataURL(blob);
}

// Starts recording from a microphone.
function StartMic()
{

	if (!navigator.getUserMedia)
	{
		navigator.getUserMedia =
			navigator.getUserMedia ||
			navigator.webkitGetUserMedia ||
			navigator.mozGetUserMedia ||
			navigator.msGetUserMedia;
	}

	if (navigator.getUserMedia)
	{

		navigator.getUserMedia(
			{ audio: true },
			function (stream)
			{
				start_microphone(stream);
			},
			function (e)
			{
				alert('Error capturing audio.');
			}
		);

	}
	else
	{
		alert('getUserMedia not supported in this browser.');
	}
}

// Callback worker for StartMic().
function start_microphone(stream)
{
	unityInstance.SendMessage("Managers", "NotifyRecordingChange", 0);

	audioContext = new AudioContext();
	microphone_stream = audioContext.createMediaStreamSource(stream);
	audioContext.audioWorklet.addModule('./recorderWorkletProcessor.js')
		.then(
			() => {
				recorderNode = new window.AudioWorkletNode(audioContext, 'recorder-worklet');

				microphone_stream.connect(recorderNode);
				recorderNode.connect(audioContext.destination);

				unityInstance.SendMessage("Managers", "NotifyRecordingChange", 2);

				recorderNode.port.onmessage =
					(e) => {
						if (e.data.eventType === 'data') {

							// https://medium.com/@koteswar.meesala/convert-array-buffer-to-base64-string-to-display-images-in-angular-7-4c443db242cd
							// https://stackoverflow.com/questions/63713889/convert-float32array-to-base64-in-javascript
							const audioData = e.data.audioBuffer;
							arrayBufferToBase64(
								audioData,
								(x) => {
									unityInstance.SendMessage("Managers", "ReceiveB64FloatPCMChunk", x);
								});
						}

						if (e.data.eventType === 'stop') {
							unityInstance.SendMessage("Managers", "NotifyRecordingChange", 1);
						}
					};

				recorderNode.parameters.get('isRecording').setValueAtTime(1, 0.0);
			})
}

// Callback
function StopMic()
{
	audioContext.close();
	recorderNode = null;

	unityInstance.SendMessage("Managers", "NotifyRecordingChange", 1);
}