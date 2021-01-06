var recorderNode = null;
var audioContext = null;
var audioInput = null;
var microphone_stream = null;
var recorder = null;

const MICRENUM_BOOTING = 0;
const MICRENUM_NOTACTIVE = 1;
const MICRENUM_RECORDING = 2;

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
	// https://www.meziantou.net/record-audio-with-javascript.htm
	
	unityInstance.SendMessage("Managers", "NotifyRecordingChange", MICRENUM_BOOTING);
	
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
				unityInstance.SendMessage("Managers", "NotifyRecordingChange", MICRENUM_NOTACTIVE);
				alert('Error capturing audio.');
			}
		);
	}
	else
	{
		alert('getUserMedia not supported in this browser.');
		unityInstance.SendMessage("Managers", "NotifyRecordingChange", MICRENUM_NOTACTIVE);
	}
}

// Callback worker for StartMic().
function start_microphone(stream)
{
	audioContext = new AudioContext();
	microphone_stream = audioContext.createMediaStreamSource(stream);
	
	var bufferSize = 2048;
	var numberOfInputChannels = 2;
	var numberOfOutputChannels = 2;
	if (audioContext.createScriptProcessor) 
	{
		recorder = audioContext.createScriptProcessor(bufferSize, numberOfInputChannels, numberOfOutputChannels);
	} 
	else 
	{
		recorder = audioContext.createJavaScriptNode(bufferSize, numberOfInputChannels, numberOfOutputChannels);
	}
	
	recorder.onaudioprocess = function (e) 
	{
		console.log("yo!");
		arrayBufferToBase64(
			e.inputBuffer.getChannelData(0),
			(x) => {
				unityInstance.SendMessage("Managers", "ReceiveB64FloatPCMChunk", x);
			});
	}

	// we connect the recorder with the input stream
	microphone_stream.connect(recorder);
	recorder.connect(audioContext.destination)
	
	unityInstance.SendMessage("Managers", "NotifyRecordingChange", MICRENUM_RECORDING);
}

// Callback
function StopMic()
{
	if(audioContext == null)
		return;
		
	recorder.disconnect(audioContext.destination);
	microphone_stream.disconnect(recorder);
	
	audioContext = null;
	recorder = null;
	microphone_stream = null;

	unityInstance.SendMessage("Managers", "NotifyRecordingChange", MICRENUM_NOTACTIVE);
}