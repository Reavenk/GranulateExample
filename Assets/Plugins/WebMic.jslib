mergeInto(LibraryManager.library, 
{

	Recording_Start: function () 
	{
		MicInit();
	},

	Recording_Stop: function()
	{
		stop_microphone();
	},

	Recording_IsRecording: function()
	{
		if(audioContext == undefined || audioContext == null)
			return false;

		if(audioContext.state != "running")
			return false;

		if(recorderNode == null)
			return false;

		return recorderNode.parameters.get('isRecording').value == 1;
	},
})