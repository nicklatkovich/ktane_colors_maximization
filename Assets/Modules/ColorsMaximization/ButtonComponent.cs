using UnityEngine;

public class ButtonComponent : MonoBehaviour {
	public TextMesh textMesh;
	public Renderer glass;

	private bool _colorblindMode = false;
	public bool colorblindMode {
		get { return _colorblindMode; }
		set {
			if (_colorblindMode == value) return;
			_colorblindMode = value;
			OnChanged();
		}
	}

	private Color _primaryColor;
	public Color primaryColor {
		get { return _primaryColor; }
		set {
			if (_primaryColor == value) return;
			_primaryColor = value;
			OnChanged();
		}
	}

	private bool _active = true;
	public bool active {
		get { return _active; }
		set {
			if (_active == value) return;
			_active = value;
			OnChanged();
		}
	}

	private void OnChanged() {
		if (!_colorblindMode || !_active) textMesh.text = "";
		else textMesh.text = ColorsMaximizationModule.colorsName[_primaryColor].Substring(0, 1);
		glass.material.SetColor("_Color", _colorblindMode || !_active ? Color.black : _primaryColor);
	}
}
