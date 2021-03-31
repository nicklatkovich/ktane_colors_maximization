using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class ColorsMaximizationModule : MonoBehaviour {
	public ButtonComponent buttonPrefab;
	public KMSelectable submitButton;
	public GameObject buttonsCollection;
	public KMAudio KMAudio;
	public KMBombInfo bomb;

	private bool _passed = false;
	private readonly Color[] _allColors = {
		Color.red,
		Color.green,
		Color.blue,
		Color.magenta,
		Color.yellow,
		Color.white,
	};
	private List<ButtonComponent> _buttons = new List<ButtonComponent>();
	private Dictionary<Color, int> _countOfColor = new Dictionary<Color, int>();

	public Color[] GetOrderedColors() {
		if (bomb.GetBatteryCount() == 4) {
			return new Color[] { Color.red, Color.green, Color.blue, Color.magenta, Color.yellow, Color.white };
		} else if (bomb.GetOffIndicators().Count() == 2) {
			return new Color[] { Color.green, Color.red, Color.yellow, Color.magenta, Color.blue, Color.white };
		} else if (bomb.GetSerialNumberNumbers().Select(e => e % 2 == 0).Count() == 2) {
			return new Color[] { Color.white, Color.blue, Color.red, Color.green, Color.magenta, Color.yellow };
		} else if (bomb.IsPortPresent(Port.Serial)) {
			return new Color[] { Color.magenta, Color.green, Color.blue, Color.red, Color.white, Color.yellow };
		} else if (bomb.GetTwoFactorCodes().Any(e => e % 3 == 0 || e % 5 == 0)) {
			return new Color[] { Color.magenta, Color.yellow, Color.white, Color.green, Color.red, Color.blue };
		}
		return new Color[] { Color.white, Color.yellow, Color.magenta, Color.blue, Color.green, Color.red };
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
		int prevPrevScore = 0;
		int prevScore = 0;
		Dictionary<Color, int> scoreOfColor = new Dictionary<Color, int>();
		for (int score = 1; score <= colors.Length; score++) {
			Color color = colors[score - 1];
			scoreOfColor[color] = score;
			int newScore = Mathf.Max(prevPrevScore + score * _countOfColor[color], prevScore);
			prevPrevScore = prevScore;
			prevScore = newScore;
		}
		int expectedScore = prevScore;
		int submitedScore = 0;
		foreach (ButtonComponent button in _buttons) {
			if (!button.active) continue;
			submitedScore += scoreOfColor[button.primaryColor];
		}
		if (submitedScore == expectedScore) {
			_passed = true;
			GetComponent<KMBombModule>().HandlePass();
		} else GetComponent<KMBombModule>().HandleStrike();
		KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
		return false;
	}
}
