// Modified from https://gist.github.com/flpvsk/047140b31c968001dc563998f7440cc1
// The original version had a flaw where every time a payload of streaming
// data was ready for processing, only the first float value would be transfered
// instead of the entire payload.

/*
A worklet for recording in sync with AudioContext.currentTime.

More info about the API:
https://developers.google.com/web/updates/2017/12/audio-worklet

How to use:

1. Serve this file from your server (e.g. put it in the "public" folder) as is.

2. Register the worklet:

    const audioContext = new AudioContext();
    audioContext.audioWorklet.addModule('path/to/recorderWorkletProcessor.js')
      .then(() => {
        // your code here
      })

3. Whenever you need to record anything, create a WorkletNode, route the 
audio into it, and schedule the values for 'isRecording' parameter:

      const recorderNode = new window.AudioWorkletNode(
        audioContext,
        'recorder-worklet'
      );

      yourSourceNode.connect(recorderNode);
      recorderNode.connect(audioContext.destination);

      recorderNode.port.onmessage = (e) => {
        if (e.data.eventType === 'data') {
          const audioData = e.data.audioBuffer;
          // process pcm data
        }

        if (e.data.eventType === 'stop') {
          // recording has stopped
        }
      };

      recorderNode.parameters.get('isRecording').setValueAtTime(1, time);
      recorderNode.parameters.get('isRecording').setValueAtTime(
        0,
        time + duration
      );
      yourSourceNode.start(time);
      
*/

class RecorderWorkletProcessor extends AudioWorkletProcessor 
{
	  static get parameterDescriptors() 
	  {
		return [{
		  name: 'isRecording',
		  defaultValue: 0
		}];
	  }

	  constructor() 
	  {
			super();
			this._bufferSize = 2048;
			this._buffer = new Float32Array(this._bufferSize);
			this._writeCursor = 0;
	  }

	_flush() 
	{
		let buffer = this._buffer;
		if (this._writeCursor < this._bufferSize) 
		{
			// While not a cheap operation (relatively speaking), the 
			// slice will rarely happen.
			buffer = buffer.slice(0, this._writeCursor);
		}

		this.port.postMessage(
			{
				eventType: 'data',
				audioBuffer: buffer
			});

		this._writeCursor = 0;
	}

	_recordingStopped() 
	{
		this.port.postMessage(
			{
				eventType: 'stop'
			});
	}

	process(inputs, outputs, parameters) 
	{
		const isRecordingValues = parameters.isRecording;

		for ( let dataIndex = 0; dataIndex < isRecordingValues.length; dataIndex++) 
		{
			const shouldRecord = isRecordingValues[dataIndex] === 1;
			if (!shouldRecord) 
			{
				if(this._writeCursor != 0)
					this._flush();
					
				this._recordingStopped();
				return;
			}

			// There are three indexable arrays, the first is the 
			// stream source, the second in the channel stream, and
			// the third is the actual PCM.
			let left = inputs[0][0].length;	// How much left to transfer?
			
			// While we still have data to transfer
			//
			// The processing of audio payload may involve multiple 
			// flushes.
			while(left > 0)
			{
				// If we're maxed, time to flush to make space.
				if(this._writeCursor == this._bufferSize)
					_flush();
					
				// There are several things that define the max boundary of what
				// we can transfer - whichever we hit first is what we obey.
				let toTrans = Math.min(left, this._bufferSize - this._writeCursor);
				this._buffer.set(inputs[0][0], this._writeCursor);
				
				left -= toTrans;
				this._writeCursor += toTrans;
			}
			
			if(this._writeCursor == this._bufferSize)
				this._flush();
		}

		return true;
	}

}

registerProcessor('recorder-worklet', RecorderWorkletProcessor);
