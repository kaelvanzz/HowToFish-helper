using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Animating;

[AddComponentMenu("FishNet/Component/NetworkAnimator")]
public sealed class NetworkAnimator : NetworkBehaviour
{
	private struct ReceivedServerData
	{
		private int _length;

		private byte[] _data;

		public ArraySegment<byte> GetArraySegment()
		{
			return new ArraySegment<byte>(_data, 0, _length);
		}

		public ReceivedServerData(ArraySegment<byte> segment)
		{
			_length = segment.Count;
			_data = ByteArrayPool.Retrieve(_length);
			Buffer.BlockCopy(segment.Array, segment.Offset, _data, 0, _length);
		}

		public void Dispose()
		{
			if (_data != null)
			{
				ByteArrayPool.Store(_data);
			}
		}
	}

	private struct StateChange
	{
		public int FrameCount;

		public bool IsCrossfade;

		public int Hash;

		public bool FixedTime;

		public float DurationTime;

		public float OffsetTime;

		public float NormalizedTransitionTime;

		public StateChange(int frame)
		{
			FrameCount = frame;
			IsCrossfade = false;
			Hash = 0;
			FixedTime = false;
			DurationTime = 0f;
			OffsetTime = 0f;
			NormalizedTransitionTime = 0f;
		}

		public StateChange(int frame, int hash, bool fixedTime, float duration, float offset, float normalizedTransition)
		{
			FrameCount = frame;
			IsCrossfade = true;
			Hash = hash;
			FixedTime = fixedTime;
			DurationTime = duration;
			OffsetTime = offset;
			NormalizedTransitionTime = normalizedTransition;
		}
	}

	private class ClientAuthoritativeUpdate
	{
		public int BufferCount;

		private int[] _bufferLengths;

		private List<byte[]> _buffers = new List<byte[]>();

		private const int MAXIMUM_DATA_SIZE = 1000;

		public const int MAXIMUM_BUFFER_COUNT = 2;

		public bool ForceAll { get; private set; }

		public ClientAuthoritativeUpdate()
		{
			for (int i = 0; i < 2; i++)
			{
				_buffers.Add(new byte[1000]);
			}
			_bufferLengths = new int[2];
		}

		public void AddToBuffer(ref ArraySegment<byte> data)
		{
			int count = data.Count;
			if (count <= 1000)
			{
				if (BufferCount >= 2)
				{
					ForceAll = true;
					return;
				}
				byte[] dst = _buffers[BufferCount];
				Buffer.BlockCopy(data.Array, data.Offset, dst, 0, count);
				_bufferLengths[BufferCount] = count;
				BufferCount++;
			}
		}

		public void GetBuffer(int index, ref byte[] buffer, ref int length)
		{
			if (index > _buffers.Count)
			{
				NetworkManagerExtensions.LogWarning("Index exceeds Buffers count.");
				return;
			}
			if (index > _bufferLengths.Length)
			{
				NetworkManagerExtensions.LogWarning("Index exceeds BufferLengths count.");
				return;
			}
			buffer = _buffers[index];
			length = _bufferLengths[index];
		}

		public void Reset()
		{
			BufferCount = 0;
			ForceAll = false;
		}
	}

	private struct SmoothedFloat
	{
		public readonly float Rate;

		public readonly float Target;

		public SmoothedFloat(float rate, float target)
		{
			Rate = rate;
			Target = target;
		}
	}

	private struct TriggerUpdate
	{
		public byte ParameterIndex;

		public bool Setting;

		public TriggerUpdate(byte parameterIndex, bool setting)
		{
			ParameterIndex = parameterIndex;
			Setting = setting;
		}
	}

	private class ParameterDetail
	{
		public readonly AnimatorControllerParameter ControllerParameter;

		public readonly byte TypeIndex;

		public readonly int Hash;

		public ParameterDetail(AnimatorControllerParameter controllerParameter, byte typeIndex)
		{
			ControllerParameter = controllerParameter;
			TypeIndex = typeIndex;
			Hash = controllerParameter.nameHash;
		}
	}

	[SerializeField]
	[HideInInspector]
	internal List<string> IgnoredParameters = new List<string>();

	[Tooltip("The animator component to synchronize.")]
	[SerializeField]
	private Animator _animator;

	[Tooltip("True to synchronize changes even when the animator component is disabled.")]
	[SerializeField]
	private bool _synchronizeWhenDisabled;

	[Tooltip("True to smooth float value changes for spectators.")]
	[SerializeField]
	private bool _smoothFloats = true;

	[Tooltip("How many ticks to interpolate.")]
	[Range(1f, 250f)]
	[SerializeField]
	private ushort _interpolation = 2;

	[Tooltip("True if using client authoritative animations.")]
	[SerializeField]
	private bool _clientAuthoritative = true;

	[Tooltip("True to synchronize server results back to owner. Typically used when you are changing animations on the server and are relying on the server response to update the clients animations.")]
	[SerializeField]
	private bool _sendToOwner;

	private readonly List<ParameterDetail> _parameterDetails = new List<ParameterDetail>();

	private readonly List<int> _ints = new List<int>();

	private readonly List<float> _floats = new List<float>();

	private readonly List<bool> _bools = new List<bool>();

	private float[] _layerWeights;

	private float _speed;

	private readonly List<TriggerUpdate> _triggerUpdates = new List<TriggerUpdate>();

	private Dictionary<int, SmoothedFloat> _smoothedFloats = new Dictionary<int, SmoothedFloat>();

	private Dictionary<int, StateChange> _unsynchronizedLayerStates = new Dictionary<int, StateChange>();

	private Animator _lastAnimator;

	private RuntimeAnimatorController _lastController;

	private PooledWriter _writer = new PooledWriter();

	private ClientAuthoritativeUpdate _clientAuthoritativeUpdates;

	private bool _forceAllOnTimed;

	private Queue<ReceivedServerData> _fromServerBuffer = new Queue<ReceivedServerData>();

	private uint _startTick;

	private bool _subscribedToTicks;

	private const byte LAYER_WEIGHT = 240;

	private const byte SPEED = 241;

	private const byte STATE = 242;

	private const byte CROSSFADE = 243;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EAnimating_002ENetworkAnimatorFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EAnimating_002ENetworkAnimatorFishNet_002ERuntime_002Edll_Excuted;

	public Animator Animator => _animator;

	public bool ClientAuthoritative => _clientAuthoritative;

	private bool _canSynchronizeAnimator
	{
		get
		{
			if (!_isAnimatorSet)
			{
				return false;
			}
			if (_animator.enabled || _synchronizeWhenDisabled)
			{
				return true;
			}
			return false;
		}
	}

	private bool _isAnimatorSet => !(_animator == null) && !(_animator.runtimeAnimatorController == null);

	private bool _canSmoothFloats
	{
		get
		{
			if (!base.IsClientStarted)
			{
				return false;
			}
			if (!_smoothFloats)
			{
				return false;
			}
			if (base.IsOwner && ClientAuthoritative)
			{
				return false;
			}
			return true;
		}
	}

	public void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EAnimating_002ENetworkAnimator_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	private void OnDestroy()
	{
		ChangeTickSubscription(subscribe: false);
	}

	[APIExclude]
	public override void OnSpawnServer(NetworkConnection connection)
	{
		if (_canSynchronizeAnimator && AnimatorUpdated(out var updatedBytes, forceAll: true))
		{
			TargetAnimatorUpdated(connection, updatedBytes);
		}
	}

	public override void OnStartNetwork()
	{
		ChangeTickSubscription(subscribe: true);
	}

	[APIExclude]
	public override void OnStartServer()
	{
		if (_clientAuthoritative)
		{
			_clientAuthoritativeUpdates = new ClientAuthoritativeUpdate();
		}
	}

	public override void OnStartClient()
	{
		base.TimeManager.OnUpdate += TimeManager_OnUpdate;
	}

	public override void OnStopClient()
	{
		if (base.TimeManager != null)
		{
			base.TimeManager.OnUpdate -= TimeManager_OnUpdate;
		}
	}

	public override void OnStopNetwork()
	{
		_unsynchronizedLayerStates.Clear();
		ChangeTickSubscription(subscribe: false);
	}

	private void ChangeTickSubscription(bool subscribe)
	{
		if (subscribe != _subscribedToTicks && !(base.NetworkManager == null))
		{
			_subscribedToTicks = subscribe;
			if (subscribe)
			{
				base.NetworkManager.TimeManager.OnPreTick += TimeManager_OnPreTick;
				base.NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
			}
			else
			{
				base.NetworkManager.TimeManager.OnPreTick -= TimeManager_OnPreTick;
				base.NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
			}
		}
	}

	private void TimeManager_OnPreTick()
	{
		if (!_canSynchronizeAnimator)
		{
			_fromServerBuffer.Clear();
		}
		else if (_startTick != 0)
		{
			if (_fromServerBuffer.Count == 0)
			{
				_startTick = 0u;
			}
			else if (base.TimeManager.LocalTick >= _startTick)
			{
				ReceivedServerData receivedServerData = _fromServerBuffer.Dequeue();
				ArraySegment<byte> updatedParameters = receivedServerData.GetArraySegment();
				ApplyParametersUpdated(ref updatedParameters);
				receivedServerData.Dispose();
			}
		}
	}

	private void TimeManager_OnPostTick()
	{
		if (_canSynchronizeAnimator)
		{
			CheckSendToServer();
			CheckSendToClients();
		}
	}

	private void TimeManager_OnUpdate()
	{
		if (_canSynchronizeAnimator && base.IsClientStarted)
		{
			SmoothFloats();
		}
	}

	private void InitializeOnce()
	{
		if (_animator == null)
		{
			_animator = GetComponent<Animator>();
		}
		if (!ApplicationState.IsPlaying() || !_canSynchronizeAnimator)
		{
			return;
		}
		_speed = _animator.speed;
		_layerWeights = new float[_animator.layerCount];
		for (int i = 0; i < _layerWeights.Length; i++)
		{
			_layerWeights[i] = _animator.GetLayerWeight(i);
		}
		_parameterDetails.Clear();
		_bools.Clear();
		_floats.Clear();
		_ints.Clear();
		AnimatorControllerParameter[] parameters = _animator.parameters;
		foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
		{
			if (_animator.IsParameterControlledByCurve(animatorControllerParameter.name))
			{
				continue;
			}
			if (_parameterDetails.Count == 240)
			{
				base.NetworkManager.LogError("Parameter " + animatorControllerParameter.name + " exceeds the allowed 240 parameter count and is being ignored.");
				continue;
			}
			int num = 0;
			if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool)
			{
				num = _bools.Count;
				_bools.Add(_animator.GetBool(animatorControllerParameter.nameHash));
			}
			else if (animatorControllerParameter.type == AnimatorControllerParameterType.Float)
			{
				num = _floats.Count;
				_floats.Add(_animator.GetFloat(animatorControllerParameter.name));
			}
			else if (animatorControllerParameter.type == AnimatorControllerParameterType.Int)
			{
				num = _ints.Count;
				_ints.Add(_animator.GetInteger(animatorControllerParameter.nameHash));
			}
			else if (animatorControllerParameter.type == AnimatorControllerParameterType.Trigger)
			{
				num = -1;
			}
			_parameterDetails.Add(new ParameterDetail(animatorControllerParameter, (byte)num));
		}
	}

	public void SetAnimator(Animator animator)
	{
		if (!(animator == _lastAnimator))
		{
			_animator = animator;
			InitializeOnce();
			_lastAnimator = animator;
		}
	}

	public void SetController(RuntimeAnimatorController controller)
	{
		if (!(controller == _lastController))
		{
			_animator.runtimeAnimatorController = controller;
			InitializeOnce();
			_lastController = controller;
		}
	}

	private void CheckSendToServer()
	{
		if (!base.IsServerStarted && base.IsClientInitialized && ClientAuthoritative && base.IsOwner)
		{
			if (AnimatorUpdated(out var updatedBytes, _forceAllOnTimed))
			{
				ServerAnimatorUpdated(updatedBytes);
			}
			_forceAllOnTimed = false;
		}
	}

	private void CheckSendToClients()
	{
		if (!base.IsServerInitialized)
		{
			return;
		}
		bool flag;
		if (ClientAuthoritative)
		{
			if (!base.Owner.IsValid)
			{
				flag = true;
			}
			else if (base.IsOwner)
			{
				flag = true;
			}
			else
			{
				if (_clientAuthoritativeUpdates.BufferCount == 0)
				{
					return;
				}
				if (_clientAuthoritativeUpdates.ForceAll)
				{
					flag = true;
					_clientAuthoritativeUpdates.Reset();
				}
				else
				{
					flag = false;
				}
			}
		}
		else
		{
			flag = true;
		}
		if (!flag)
		{
			byte[] buffer = null;
			int length = 0;
			for (int i = 0; i < _clientAuthoritativeUpdates.BufferCount; i++)
			{
				_clientAuthoritativeUpdates.GetBuffer(i, ref buffer, ref length);
				if (buffer != null && length != 0)
				{
					SendSegment(new ArraySegment<byte>(buffer, 0, length));
				}
			}
			_clientAuthoritativeUpdates.Reset();
		}
		else
		{
			if (AnimatorUpdated(out var updatedBytes, _forceAllOnTimed))
			{
				SendSegment(updatedBytes);
			}
			_forceAllOnTimed = false;
		}
		void SendSegment(ArraySegment<byte> data)
		{
			foreach (NetworkConnection observer in base.Observers)
			{
				if (_sendToOwner || !(observer == base.Owner))
				{
					TargetAnimatorUpdated(observer, data);
				}
			}
		}
	}

	private void SmoothFloats()
	{
		if (!_canSmoothFloats || _smoothedFloats.Count == 0)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, SmoothedFloat> smoothedFloat in _smoothedFloats)
		{
			float num = Mathf.MoveTowards(_animator.GetFloat(smoothedFloat.Key), smoothedFloat.Value.Target, smoothedFloat.Value.Rate * deltaTime);
			_animator.SetFloat(smoothedFloat.Key, num);
			if (num == smoothedFloat.Value.Target)
			{
				list.Add(smoothedFloat.Key);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			_smoothedFloats.Remove(list[i]);
		}
	}

	private bool AnimatorUpdated(out ArraySegment<byte> updatedBytes, bool forceAll = false)
	{
		updatedBytes = default(ArraySegment<byte>);
		if (_layerWeights == null)
		{
			return false;
		}
		_writer.Clear();
		for (byte b = 0; b < _parameterDetails.Count; b++)
		{
			ParameterDetail parameterDetail = _parameterDetails[b];
			if (parameterDetail.ControllerParameter.type == AnimatorControllerParameterType.Bool)
			{
				bool @bool = _animator.GetBool(parameterDetail.Hash);
				if (forceAll || _bools[parameterDetail.TypeIndex] != @bool)
				{
					_writer.WriteUInt8Unpacked(b);
					_writer.WriteBoolean(@bool);
					_bools[parameterDetail.TypeIndex] = @bool;
				}
			}
			else if (parameterDetail.ControllerParameter.type == AnimatorControllerParameterType.Float)
			{
				float @float = _animator.GetFloat(parameterDetail.Hash);
				if (forceAll || _floats[parameterDetail.TypeIndex] != @float)
				{
					_writer.WriteUInt8Unpacked(b);
					_writer.WriteSingle(@float);
					_floats[parameterDetail.TypeIndex] = @float;
				}
			}
			else if (parameterDetail.ControllerParameter.type == AnimatorControllerParameterType.Int)
			{
				int integer = _animator.GetInteger(parameterDetail.Hash);
				if (forceAll || _ints[parameterDetail.TypeIndex] != integer)
				{
					_writer.WriteUInt8Unpacked(b);
					_writer.WriteInt32(integer);
					_ints[parameterDetail.TypeIndex] = integer;
				}
			}
		}
		for (int i = 0; i < _triggerUpdates.Count; i++)
		{
			_writer.WriteUInt8Unpacked(_triggerUpdates[i].ParameterIndex);
			_writer.WriteBoolean(_triggerUpdates[i].Setting);
		}
		_triggerUpdates.Clear();
		if (forceAll)
		{
			for (int j = 0; j < _animator.layerCount; j++)
			{
				_unsynchronizedLayerStates[j] = new StateChange(Time.frameCount);
			}
		}
		if (_unsynchronizedLayerStates.Count > 0)
		{
			int frameCount = Time.frameCount;
			List<int> list = CollectionCaches<int>.RetrieveList();
			foreach (KeyValuePair<int, StateChange> unsynchronizedLayerState in _unsynchronizedLayerStates)
			{
				if (frameCount == unsynchronizedLayerState.Value.FrameCount)
				{
					continue;
				}
				list.Add(unsynchronizedLayerState.Key);
				int key = unsynchronizedLayerState.Key;
				StateChange value = unsynchronizedLayerState.Value;
				if (!value.IsCrossfade)
				{
					if (ReturnCurrentLayerState(out var stateHash, out var normalizedTime, key))
					{
						_writer.WriteUInt8Unpacked(242);
						_writer.WriteUInt8Unpacked((byte)key);
						_writer.WriteInt32Unpacked(stateHash);
						_writer.WriteSingle(normalizedTime);
					}
				}
				else
				{
					_writer.WriteUInt8Unpacked(243);
					_writer.WriteUInt8Unpacked((byte)key);
					_writer.WriteInt32(value.Hash);
					_writer.WriteBoolean(value.FixedTime);
					_writer.WriteSingle(value.DurationTime);
					_writer.WriteSingle(value.OffsetTime);
					_writer.WriteSingle(value.NormalizedTransitionTime);
				}
			}
			if (list.Count > 0)
			{
				for (int k = 0; k < list.Count; k++)
				{
					_unsynchronizedLayerStates.Remove(list[k]);
				}
				CollectionCaches<int>.Store(list);
			}
		}
		for (int l = 0; l < _layerWeights.Length; l++)
		{
			float layerWeight = _animator.GetLayerWeight(l);
			if (forceAll || _layerWeights[l] != layerWeight)
			{
				_writer.WriteUInt8Unpacked(240);
				_writer.WriteUInt8Unpacked((byte)l);
				_writer.WriteSingle(layerWeight);
				_layerWeights[l] = layerWeight;
			}
		}
		float speed = _animator.speed;
		if (forceAll || _speed != speed)
		{
			_writer.WriteUInt8Unpacked(241);
			_writer.WriteSingle(speed);
			_speed = speed;
		}
		if (_writer.Position == 0)
		{
			return false;
		}
		updatedBytes = _writer.GetArraySegment();
		return true;
	}

	private void ApplyParametersUpdated(ref ArraySegment<byte> updatedParameters)
	{
		if (!_canSynchronizeAnimator || _layerWeights == null || updatedParameters.Count == 0)
		{
			return;
		}
		PooledReader pooledReader = ReaderPool.Retrieve(updatedParameters, base.NetworkManager);
		try
		{
			while (pooledReader.Remaining > 0)
			{
				byte b = pooledReader.ReadUInt8Unpacked();
				switch (b)
				{
				case 240:
				{
					byte layerIndex = pooledReader.ReadUInt8Unpacked();
					float weight = pooledReader.ReadSingle();
					_animator.SetLayerWeight(layerIndex, weight);
					continue;
				}
				case 241:
				{
					float speed = pooledReader.ReadSingle();
					_animator.speed = speed;
					continue;
				}
				case 242:
				{
					byte layer2 = pooledReader.ReadUInt8Unpacked();
					int stateNameHash = pooledReader.ReadInt32Unpacked();
					float normalizedTime = pooledReader.ReadSingle();
					_animator.Play(stateNameHash, layer2, normalizedTime);
					continue;
				}
				case 243:
				{
					byte layer = pooledReader.ReadUInt8Unpacked();
					int stateHashName = pooledReader.ReadInt32();
					bool flag = pooledReader.ReadBoolean();
					float num = pooledReader.ReadSingle();
					float num2 = pooledReader.ReadSingle();
					float normalizedTransitionTime = pooledReader.ReadSingle();
					if (flag)
					{
						_animator.CrossFadeInFixedTime(stateHashName, num, layer, num2, normalizedTransitionTime);
					}
					else
					{
						_animator.CrossFade(stateHashName, num, layer, num2, normalizedTransitionTime);
					}
					continue;
				}
				}
				AnimatorControllerParameterType type = _parameterDetails[b].ControllerParameter.type;
				switch (type)
				{
				case AnimatorControllerParameterType.Bool:
				{
					bool value = pooledReader.ReadBoolean();
					_animator.SetBool(_parameterDetails[b].Hash, value);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					float num3 = pooledReader.ReadSingle();
					if (_canSmoothFloats)
					{
						float @float = _animator.GetFloat(_parameterDetails[b].Hash);
						float num4 = (float)base.TimeManager.TickDelta;
						float rate = Mathf.Abs(@float - num3) / num4;
						_smoothedFloats[_parameterDetails[b].Hash] = new SmoothedFloat(rate, num3);
					}
					else
					{
						_animator.SetFloat(_parameterDetails[b].Hash, num3);
					}
					break;
				}
				case AnimatorControllerParameterType.Int:
				{
					int value2 = pooledReader.ReadInt32();
					_animator.SetInteger(_parameterDetails[b].Hash, value2);
					break;
				}
				case AnimatorControllerParameterType.Trigger:
					if (pooledReader.ReadBoolean())
					{
						_animator.SetTrigger(_parameterDetails[b].Hash);
					}
					else
					{
						_animator.ResetTrigger(_parameterDetails[b].Hash);
					}
					break;
				default:
					base.NetworkManager.LogWarning($"Unhandled parameter type of {type}.");
					break;
				}
			}
		}
		catch
		{
			base.NetworkManager.LogWarning("An error occurred while applying updates. This may occur when malformed data is sent or when you change the animator or controller but not on all connections.");
		}
		finally
		{
			pooledReader?.Store();
		}
	}

	private bool ReturnCurrentLayerState(out int stateHash, out float normalizedTime, int layerIndex)
	{
		stateHash = 0;
		normalizedTime = 0f;
		if (!_canSynchronizeAnimator)
		{
			return false;
		}
		AnimatorStateInfo currentAnimatorStateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
		stateHash = currentAnimatorStateInfo.fullPathHash;
		normalizedTime = currentAnimatorStateInfo.normalizedTime;
		return stateHash != 0;
	}

	public void SendAll()
	{
		_forceAllOnTimed = true;
	}

	public void Play(string name)
	{
		Play(Animator.StringToHash(name));
	}

	public void Play(int hash)
	{
		for (int i = 0; i < _animator.layerCount; i++)
		{
			Play(hash, i, 0f);
		}
	}

	public void Play(string name, int layer)
	{
		Play(Animator.StringToHash(name), layer);
	}

	public void Play(int hash, int layer)
	{
		Play(hash, layer, 0f);
	}

	public void Play(string name, int layer, float normalizedTime)
	{
		Play(Animator.StringToHash(name), layer, normalizedTime);
	}

	public void Play(int hash, int layer, float normalizedTime)
	{
		if (_canSynchronizeAnimator && (_animator.HasState(layer, hash) || hash == 0))
		{
			_animator.Play(hash, layer, normalizedTime);
			_unsynchronizedLayerStates[layer] = new StateChange(Time.frameCount);
		}
	}

	public void PlayInFixedTime(string name, float fixedTime)
	{
		PlayInFixedTime(Animator.StringToHash(name), fixedTime);
	}

	public void PlayInFixedTime(int hash, float fixedTime)
	{
		for (int i = 0; i < _animator.layerCount; i++)
		{
			PlayInFixedTime(hash, i, fixedTime);
		}
	}

	public void PlayInFixedTime(string name, int layer, float fixedTime)
	{
		PlayInFixedTime(Animator.StringToHash(name), layer, fixedTime);
	}

	public void PlayInFixedTime(int hash, int layer, float fixedTime)
	{
		if (_canSynchronizeAnimator && (_animator.HasState(layer, hash) || hash == 0))
		{
			_animator.PlayInFixedTime(hash, layer, fixedTime);
			_unsynchronizedLayerStates[layer] = new StateChange(Time.frameCount);
		}
	}

	public void CrossFade(string stateName, float normalizedTransitionDuration, int layer, float normalizedTimeOffset = float.NegativeInfinity, float normalizedTransitionTime = 0f)
	{
		CrossFade(Animator.StringToHash(stateName), normalizedTransitionDuration, layer, normalizedTimeOffset, normalizedTransitionTime);
	}

	public void CrossFade(int hash, float normalizedTransitionDuration, int layer, float normalizedTimeOffset = 0f, float normalizedTransitionTime = 0f)
	{
		if (_canSynchronizeAnimator && (_animator.HasState(layer, hash) || hash == 0))
		{
			_animator.CrossFade(hash, normalizedTransitionDuration, layer, normalizedTimeOffset, normalizedTransitionTime);
			_unsynchronizedLayerStates[layer] = new StateChange(Time.frameCount, hash, fixedTime: false, normalizedTransitionDuration, normalizedTimeOffset, normalizedTransitionTime);
		}
	}

	public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer, float fixedTimeOffset = 0f, float normalizedTransitionTime = 0f)
	{
		CrossFadeInFixedTime(Animator.StringToHash(stateName), fixedTransitionDuration, layer, fixedTimeOffset, normalizedTransitionTime);
	}

	public void CrossFadeInFixedTime(int hash, float fixedTransitionDuration, int layer, float fixedTimeOffset = 0f, float normalizedTransitionTime = 0f)
	{
		if (_canSynchronizeAnimator && (_animator.HasState(layer, hash) || hash == 0))
		{
			_animator.CrossFadeInFixedTime(hash, fixedTransitionDuration, layer, fixedTimeOffset, normalizedTransitionTime);
			_unsynchronizedLayerStates[layer] = new StateChange(Time.frameCount, hash, fixedTime: true, fixedTransitionDuration, fixedTimeOffset, normalizedTransitionTime);
		}
	}

	public void SetTrigger(int hash)
	{
		if (_canSynchronizeAnimator)
		{
			UpdateTrigger(hash, set: true);
		}
	}

	public void SetTrigger(string name)
	{
		SetTrigger(Animator.StringToHash(name));
	}

	public void ResetTrigger(int hash)
	{
		UpdateTrigger(hash, set: false);
	}

	public void ResetTrigger(string name)
	{
		ResetTrigger(Animator.StringToHash(name));
	}

	private void UpdateTrigger(int hash, bool set)
	{
		if (!_canSynchronizeAnimator)
		{
			return;
		}
		bool clientAuthoritative = ClientAuthoritative;
		if (base.Owner.IsValid)
		{
			if (clientAuthoritative && !base.IsOwner)
			{
				return;
			}
		}
		else if (!base.IsServerStarted)
		{
			return;
		}
		if (set)
		{
			_animator.SetTrigger(hash);
		}
		else
		{
			_animator.ResetTrigger(hash);
		}
		if ((!clientAuthoritative || !base.IsOwner) && (!clientAuthoritative || base.Owner.IsValid) && (clientAuthoritative || !base.IsServerStarted))
		{
			return;
		}
		for (byte b = 0; b < _parameterDetails.Count; b++)
		{
			if (_parameterDetails[b].Hash == hash)
			{
				_triggerUpdates.Add(new TriggerUpdate(b, set));
				return;
			}
		}
		base.NetworkManager.LogWarning($"Hash {hash} not found while trying to update a trigger.");
	}

	[TargetRpc(ValidateTarget = false)]
	private void TargetAnimatorUpdated(NetworkConnection connection, ArraySegment<byte> data)
	{
		RpcWriter___TargetAnimatorUpdated___2304494427(connection, data);
	}

	[ServerRpc]
	private void ServerAnimatorUpdated(ArraySegment<byte> data)
	{
		RpcWriter___ServerAnimatorUpdated___415360332(data);
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EAnimating_002ENetworkAnimatorFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EAnimating_002ENetworkAnimatorFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			RegisterTargetRpc(0u, RpcReader___TargetAnimatorUpdated___2304494427);
			RegisterServerRpc(1u, RpcReader___ServerAnimatorUpdated___415360332);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EAnimating_002ENetworkAnimatorFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EAnimating_002ENetworkAnimatorFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void RpcWriter___TargetAnimatorUpdated___2304494427(NetworkConnection connection, ArraySegment<byte> data)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteArraySegmentAndSize(pooledWriter, data);
		SendTargetRpc(0u, pooledWriter, channel, DataOrderType.Default, connection, excludeServer: false, validateTarget: false);
		pooledWriter.Store();
	}

	private void RpcLogic___TargetAnimatorUpdated___2304494427(NetworkConnection P_0, ArraySegment<byte> P_1)
	{
		if (!_canSynchronizeAnimator || (base.IsServerInitialized && P_0.IsLocalClient))
		{
			return;
		}
		bool clientAuthoritative = ClientAuthoritative;
		bool isOwner = base.IsOwner;
		if (!(clientAuthoritative && isOwner) && !(!clientAuthoritative && !_sendToOwner && isOwner))
		{
			ReceivedServerData item = new ReceivedServerData(P_1);
			_fromServerBuffer.Enqueue(item);
			if (_startTick == 0)
			{
				_startTick = base.TimeManager.LocalTick + _interpolation;
			}
		}
	}

	private void RpcReader___TargetAnimatorUpdated___2304494427(PooledReader PooledReader0, Channel channel)
	{
		ArraySegment<byte> arraySegment = GeneratedReaders___Internal.InstancedExtension___ReadArraySegmentAndSize(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___TargetAnimatorUpdated___2304494427(base.LocalConnection, arraySegment);
		}
	}

	private void RpcWriter___ServerAnimatorUpdated___415360332(ArraySegment<byte> data)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteArraySegmentAndSize(pooledWriter, data);
		SendServerRpc(1u, pooledWriter, channel, DataOrderType.Default);
		pooledWriter.Store();
	}

	private void RpcLogic___ServerAnimatorUpdated___415360332(ArraySegment<byte> P_0)
	{
		if (_canSynchronizeAnimator)
		{
			if (!ClientAuthoritative)
			{
				base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
				return;
			}
			ApplyParametersUpdated(ref P_0);
			_clientAuthoritativeUpdates.AddToBuffer(ref P_0);
		}
	}

	private void RpcReader___ServerAnimatorUpdated___415360332(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		ArraySegment<byte> arraySegment = GeneratedReaders___Internal.InstancedExtension___ReadArraySegmentAndSize(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___ServerAnimatorUpdated___415360332(arraySegment);
		}
	}

	private void Awake_UserLogic_FishNet_002EComponent_002EAnimating_002ENetworkAnimator_FishNet_002ERuntime_002Edll()
	{
		InitializeOnce();
	}
}
