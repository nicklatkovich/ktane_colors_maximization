using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class ColorsMaximizationModule : MonoBehaviour {
	public static readonly Color[] allColors = {
		Color.red,
		Color.green,
		Color.blue,
		Color.magenta,
		Color.yellow,
		Color.white,
	};

	public static readonly Color[][] rules = {
		new Color[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.white },
		new Color[] { Color.green, Color.red, Color.yellow, Color.magenta, Color.blue, Color.white },
		new Color[] { Color.white, Color.blue, Color.red, Color.green, Color.magenta, Color.yellow },
		new Color[] { Color.magenta, Color.green, Color.blue, Color.red, Color.white, Color.yellow },
		new Color[] { Color.magenta, Color.yellow, Color.white, Color.green, Color.red, Color.blue },
		new Color[] { Color.white, Color.yellow, Color.magenta, Color.blue, Color.green, Color.red },
	};

	public static readonly Dictionary<Color, string> colorsName = new Dictionary<Color, string> {
		{ Color.red, "Red" },
		{ Color.green, "Green" },
		{ Color.blue, "Blue" },
		{ Color.magenta, "Magenta" },
		{ Color.yellow, "Yellow" },
		{ Color.white, "White" },
	};

	private static int _moduleIdCounter = 1;

	public const int WIDTH = 5;
	public const int HEIGHT = 4;

	public readonly string TwitchHelpMessage = string.Join(" | ", new string[] {
		"`!{0} a1 2;2 15 submit` - press buttons on coordinates, by number in reading order or with label `submit`",
		"`!{0} colorblind` enable/disable colorblind mode",
	});

	public ButtonComponent buttonPrefab;
	public KMSelectable submitButton;
	public GameObject buttonsCollection;
	public KMAudio KMAudio;
	public TextMesh submitText;
	public KMBombInfo bomb;
	public KMColorblindMode colorblindMode;

	private bool _colorblindModeEnabled = false;
	public bool colorblindModeEnabled {
		get { return _colorblindModeEnabled; }
		set {
			if (value == _colorblindModeEnabled) return;
			_colorblindModeEnabled = value;
			_buttons.ForEach(b => b.colorblindMode = value);
			submitText.color = value ? Color.white : Color.red;
		}
	}

	private bool _passed = false;
	private int _moduleId;
	private List<ButtonComponent> _buttons = new List<ButtonComponent>();
	private ButtonComponent[][] _buttonsGrid = new ButtonComponent[0][] { };
	private Dictionary<Color, int> _countOfColor = new Dictionary<Color, int>();

	private void Start() {
		colorblindModeEnabled = colorblindMode.ColorblindModeActive;
		_moduleId = _moduleIdCounter++;
		KMSelectable selfSelectable = GetComponent<KMSelectable>();
		foreach (Color color in allColors) _countOfColor[color] = 0;
		List<KMSelectable> children = new List<KMSelectable>();
		HashSet<Color> colorsActiveAtStart = getHalfRandomColors();
		_buttonsGrid = new ButtonComponent[WIDTH][];
		for (int x = 0; x < WIDTH; x++) {
			_buttonsGrid[x] = new ButtonComponent[HEIGHT];
			for (int z = 0; z < HEIGHT; z++) {
				ButtonComponent button = Instantiate(buttonPrefab);
				button.transform.parent = buttonsCollection.transform;
				button.transform.localPosition = new Vector3(x * 0.02f, 0.015f, (HEIGHT - z - 1) * 0.02f);
				button.transform.localScale = new Vector3(0.0101f, 0.01f, 0.0101f);
				button.transform.localEulerAngles = Vector3.zero;
				Color color = allColors[Random.Range(0, allColors.Length)];
				button.primaryColor = color;
				_countOfColor[color] += 1;
				button.active = colorsActiveAtStart.Contains(color);
				button.colorblindMode = colorblindMode.ColorblindModeActive;
				_buttons.Add(button);
				KMSelectable selectableButton = button.GetComponent<KMSelectable>();
				selectableButton.OnInteract += () => OnButtonPressed(button);
				selectableButton.Parent = selfSelectable;
				children.Add(selectableButton);
				_buttonsGrid[x][z] = button;
			}
		}
		foreach (Color color in allColors) {
			string prettier = colorsName[color];
			int count = _countOfColor[color];
			Debug.LogFormat("[Colors Maximization #{0}] {1} color count = {2}", _moduleId, prettier, count);
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

	public KMSelectable[] ProcessTwitchCommand(string command) {
		command = command.Trim().ToLower();
		if (command == "colorblind") {
			colorblindModeEnabled = !colorblindModeEnabled;
			return new KMSelectable[0];
		}
		KMSelectable[] parsedCoords = parseButtonsSet(command);
		if (parsedCoords != null) return parsedCoords;
		return null;
	}

	public Color[] GetOrderedColors() {
		if (bomb.GetBatteryCount() == 4) return useRule(1);
		else if (bomb.GetOffIndicators().Count() == 2) return useRule(2);
		else if (bomb.GetSerialNumberNumbers().Where(e => e % 2 == 0).Count() == 2) return useRule(3);
		else if (bomb.IsPortPresent(Port.Serial)) return useRule(4);
		else if (bomb.GetTwoFactorCodes().Any(e => e % 3 == 0 || e % 5 == 0)) return useRule(5);
		return useRule(6);
	}

	public Color[] useRule(int ruleNumber) {
		Debug.LogFormat("[Colors Maximization #{0}] Using rule #{1}", _moduleId, ruleNumber);
		Color[] result = rules[ruleNumber - 1];
		for (int i = 0; i < result.Length; i++) {
			string prettier = colorsName[result[i]];
			Debug.LogFormat("[Colors Maximization #{0}] {1} color score = {2}", _moduleId, prettier, i + 1);
		}
		return result;
	}

	public HashSet<Color> getHalfRandomColors() {
		HashSet<Color> result = new HashSet<Color>();
		Color[] colors = allColors.Clone() as Color[];
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

	public KMSelectable[] parseButtonsSet(string set) {
		string[] strings = set.Split(' ').Where(r => r.Length > 0).ToArray();
		KMSelectable[] result = new KMSelectable[strings.Count()];
		for (int i = 0; i < strings.Count(); i++) {
			KMSelectable selectable = parseButton(strings[i]);
			if (selectable == null) return null;
			result[i] = selectable;
		}
		return result;
	}

	public KMSelectable parseButton(string str) {
		if (str == "submit") return submitButton;
		Match match = Regex.Match(str, @"([a-e])([1-4])");
		if (match.Success) {
			int column = match.Groups[1].Value[0] - 'a';
			return _buttonsGrid[column][int.Parse(match.Groups[2].Value) - 1].GetComponent<KMSelectable>();
		}
		match = Regex.Match(str, @"([1-5])[;,]([1-4])");
		if (match.Success) {
			int column = int.Parse(match.Groups[1].Value) - 1;
			return _buttonsGrid[column][int.Parse(match.Groups[2].Value) - 1].GetComponent<KMSelectable>();
		}
		if (Regex.IsMatch(str, @"0|[1-9]\d*")) {
			int index = int.Parse(str) - 1;
			int row = index / WIDTH;
			if (row >= HEIGHT) return null;
			return _buttonsGrid[index % WIDTH][row].GetComponent<KMSelectable>();
		}
		return null;
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
		Debug.LogFormat("[Colors Maximization #{0}] Expected score: {1}", _moduleId, expectedScore);
		int submitedScore = 0;
		foreach (ButtonComponent button in _buttons) {
			if (!button.active) continue;
			submitedScore += scoreOfColor[button.primaryColor];
		}
		Debug.LogFormat("[Colors Maximization #{0}] Submited score: {1}", _moduleId, submitedScore);
		if (submitedScore == expectedScore) {
			_passed = true;
			GetComponent<KMBombModule>().HandlePass();
		} else GetComponent<KMBombModule>().HandleStrike();
		KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		return false;
	}

	private void LogRule(int ruleNumber) {
		Debug.LogFormat("[Colors Maximization #{0}] Using rule #{1}", _moduleId, ruleNumber);
	}
}
