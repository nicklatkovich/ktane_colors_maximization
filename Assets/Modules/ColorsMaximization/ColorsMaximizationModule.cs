using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class ColorsMaximizationModule : MonoBehaviour {
	private static int _moduleIdCounter = 1;

	public ButtonComponent buttonPrefab;
	public KMSelectable submitButton;
	public GameObject buttonsCollection;
	public KMAudio KMAudio;
	public KMBombInfo bomb;

	private bool _passed = false;
	private int _moduleId;
	private readonly Color[] _allColors = {
		Color.red,
		Color.green,
		Color.blue,
		Color.magenta,
		Color.yellow,
		Color.white,
	};
	private readonly Color[][] _rules = {
		new Color[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.white },
		new Color[] { Color.green, Color.red, Color.yellow, Color.magenta, Color.blue, Color.white },
		new Color[] { Color.white, Color.blue, Color.red, Color.green, Color.magenta, Color.yellow },
		new Color[] { Color.magenta, Color.green, Color.blue, Color.red, Color.white, Color.yellow },
		new Color[] { Color.magenta, Color.yellow, Color.white, Color.green, Color.red, Color.blue },
		new Color[] { Color.white, Color.yellow, Color.magenta, Color.blue, Color.green, Color.red },
	};
	private List<ButtonComponent> _buttons = new List<ButtonComponent>();
	private Dictionary<Color, int> _countOfColor = new Dictionary<Color, int>();
	private Dictionary<Color, string> _colorPrettier = new Dictionary<Color, string> {
		{ Color.red, "Red" },
		{ Color.green, "Green" },
		{ Color.blue, "Blue" },
		{ Color.magenta, "Magenta" },
		{ Color.yellow, "Yellow" },
		{ Color.white, "White" },
	};

	public Color[] GetOrderedColors() {
		if (bomb.GetBatteryCount() == 4) return useRule(1);
		else if (bomb.GetOffIndicators().Count() == 2) return useRule(2);
		else if (bomb.GetSerialNumberNumbers().Where(e => e % 2 == 0).Count() == 2) return useRule(3);
		else if (bomb.IsPortPresent(Port.Serial)) return useRule(4);
		else if (bomb.GetTwoFactorCodes().Any(e => e % 3 == 0 || e % 5 == 0)) return useRule(5);
		return useRule(6);
	}

	private Color[] useRule(int ruleNumber) {
		Debug.LogFormat("[ColorsMaximization #{0}] Using rule #{1}", _moduleId, ruleNumber);
		Color[] result = _rules[ruleNumber - 1];
		for (int i = 0; i < result.Length; i++) {
			string prettier = _colorPrettier[result[i]];
			Debug.LogFormat("[ColorsMaximization #{0}] {1} color score = {2}", _moduleId, prettier, i + 1);
		}
		return result;
	}

	private HashSet<Color> getHalfRandomColors() {
		HashSet<Color> result = new HashSet<Color>();
		Color[] colors = _allColors.Clone() as Color[];
		for (int i = 0; i < colors.Length; i++) {
			int pos = Random.Range(0, colors.Length - 1);
			Color tmp = colors[i];
			colors[i] = colors[pos];
			colors[pos] = tmp;
		}
		int size = Random.Range(colors.Length / 2, colors.Length / 2 + 1 + (colors.Length % 2 == 0 ? 0 : 1));
		for (int i = 0; i < size; i++) result.Add(colors[i]);
		return result;
	}

	private void Start() {
		_moduleId = _moduleIdCounter++;
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		foreach (Color color in _allColors) _countOfColor[color] = 0;
		List<KMSelectable> children = new List<KMSelectable>();
		HashSet<Color> colorsActiveAtStart = getHalfRandomColors();
		for (int x = 0; x < 5; x++) {
			for (int z = 0; z < 4; z++) {
				ButtonComponent button = Instantiate(buttonPrefab);
				button.transform.parent = buttonsCollection.transform;
				button.transform.localPosition = new Vector3(x * 0.02f, 0.015f, z * 0.02f);
				button.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
				button.transform.localEulerAngles = Vector3.zero;
				Color color = _allColors[Random.Range(0, _allColors.Length)];
				button.primaryColor = color;
				_countOfColor[color] += 1;
				button.active = colorsActiveAtStart.Contains(color);
				_buttons.Add(button);
				KMSelectable selectableButton = button.GetComponent<KMSelectable>();
				selectableButton.OnInteract += () => OnButtonPressed(button);
				selectableButton.Parent = selfSelectable;
				children.Add(selectableButton);
			}
		}
		foreach (Color color in _allColors) {
			string prettier = _colorPrettier[color];
			int count = _countOfColor[color];
			Debug.LogFormat("[ColorsMaximization #{0}] {1} color count = {2}", _moduleId, prettier, count);
		}
		children.Add(submitButton);
		selfSelectable.Children = children.ToArray();
		selfSelectable.UpdateChildren();
		submitButton.OnInteract += OnSubmitPressed;
	}

	private bool OnButtonPressed(ButtonComponent button) {
		if (_passed) return false;
		Color color = button.primaryColor;
		bool activate = !button.active;
		foreach (ButtonComponent otherButton in _buttons) {
			if (otherButton.primaryColor == color) otherButton.active = activate;
		}
		KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		return false;
	}

	private bool OnSubmitPressed() {
		if (_passed) return false;
		Color[] colors = GetOrderedColors();
		Dictionary<Color, int> scoreOfColor = new Dictionary<Color, int>();
		int[] scores = new int[6];
		for (int i = 0; i < colors.Length; i++) {
			int score = i + 1;
			Color color = colors[i];
			scoreOfColor[color] = score;
			int totalColorScore = score * _countOfColor[color];
			scores[i] = totalColorScore;
			if (i > 1) scores[i] += scores[i - 2];
			if (i > 0) scores[i] = Mathf.Max(scores[i], scores[i - 1]);
		}
		int expectedScore = Mathf.Max(scores[colors.Length - 1], scores[colors.Length - 2]);
		Debug.LogFormat("[ColorsMaximization #{0}] Expected score: {1}", _moduleId, expectedScore);
		int submitedScore = 0;
		foreach (ButtonComponent button in _buttons) {
			if (!button.active) continue;
			submitedScore += scoreOfColor[button.primaryColor];
		}
		Debug.LogFormat("[ColorsMaximization #{0}] Submited score: {1}", _moduleId, submitedScore);
		if (submitedScore == expectedScore) {
			_passed = true;
			GetComponent<KMBombModule>().HandlePass();
		} else GetComponent<KMBombModule>().HandleStrike();
		KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		return false;
	}

	private void LogRule(int ruleNumber) {
		Debug.LogFormat("[ColorsMaximization #{0}] Using rule #{1}", _moduleId, ruleNumber);
	}
}
