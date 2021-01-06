// Base code for listening to microphone PCM is taken from
// https://www.meziantou.net/record-audio-with-javascript.htm

var audioContext = null;
var audioInput = null;
var microphone_stream = null;
var recorder = null;

// The pointer to write PCM data directly into the Unity app's
// memory heap.
var floatPCMPointer = -1;

// These need to match the enums in the WebMic.cs file
const MicState =
{
	Booting: 	0,
	NotActive: 	1,
	Recording: 	2
}

// Starts recording from a microphone.
// called from WebMic.jslib's Recording_Start()
function StartMic()
{
	unityInstance.SendMessage("Managers", "NotifyRecordingChange", MicState.Booting);
	
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
				unityInstance.SendMessage("Managers", "NotifyRecordingChange", MicState.NotActive);
				alert('Error capturing audio.');
			}
		);
	}
	else
	{
		alert('getUserMedia not supported in this browser.');
		unityInstance.SendMessage("Managers", "NotifyRecordingChange", MicState.NotActive);
	}
}

// Callback worker for StartMic().
function start_microphone(stream)
{
	// Match Unity's. Personal testing shows it default to 48000
	audioContext = new AudioContext({"sampleRate": 44100});
	microphone_stream = audioContext.createMediaStreamSource(stream);

	const bufferSize = 2048;			// This must agree with the buffer size for WebMic
	const numberOfInputChannels = 1;	// Mono audio
	const numberOfOutputChannels = 1;	// Don't care about this
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
		dstPtr = floatPCMPointer;
		floatPCM = e.inputBuffer.getChannelData(0);
		unityInstance.SendMessage("Managers", "LogWrittenBuffer", floatPCM.length);

		writeTarg = new Float32Array(unityInstance.Module.HEAP8.buffer, dstPtr, bufferSize);
		writeTarg.set(floatPCM);
	}

	// we connect the recorder with the input stream
	microphone_stream.connect(recorder);
	recorder.connect(audioContext.destination)

	unityInstance.SendMessage("Managers", "NotifyRecordingChange", MicState.Recording);
}

// called from WebMic.jslib's Recording_Stop()
function StopMic()
{
	if(audioContext == null)
		return;
		
	recorder.disconnect(audioContext.destination);
	microphone_stream.disconnect(recorder);
	
	audioContext = null;
	recorder = null;
	microphone_stream = null;

	unityInstance.SendMessage("Managers", "NotifyRecordingChange", MicState.NotActive);
}