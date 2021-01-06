mergeInto(LibraryManager.library, 
{

	Recording_Start: function () { StartMic(); },
	Recording_Stop: function() { StopMic();},

	Recording_UpdatePointer: function(idx)
	{
		floatPCMPointer = idx;
	}
})