using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.NetEq;

public static class NetEqInterop
{
	private const string DllName = "meta_voice_chat_neteq.dll";

	[DllImport("meta_voice_chat_neteq.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "create_neteq")]
	public static extern IntPtr CreateNetEq(uint sampleRate, byte channels, int maxPacketsInBuffer, uint maxDelayMs, uint minDelayMs, uint additionalDelayMs);

	[DllImport("meta_voice_chat_neteq.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "free_neteq")]
	public static extern void FreeNetEq(IntPtr ptr);

	[DllImport("meta_voice_chat_neteq.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "insert_packet")]
	public static extern void InsertPacket(IntPtr ptr, ushort sequenceNumber, uint timestamp, IntPtr samples, int samplesLength, uint sampleRate, byte channels, uint durationMs);

	[DllImport("meta_voice_chat_neteq.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "get_audio")]
	public static extern int GetAudio(IntPtr ptr, IntPtr samples, int samplesLength);

	[DllImport("meta_voice_chat_neteq.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "current_buffer_size_ms")]
	public static extern uint CurrentBufferSizeMs(IntPtr ptr);
}
