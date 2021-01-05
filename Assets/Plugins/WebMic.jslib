mergeInto(LibraryManager.library, 
{

	Recording_Start: function () 
	{
		StartMic();
	},

	Recording_Stop: function()
	{
		StopMic();
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