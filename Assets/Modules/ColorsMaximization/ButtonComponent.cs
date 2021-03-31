using UnityEngine;

public class ButtonComponent : MonoBehaviour {
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

	void OnChanged() {
		Renderer renderer = transform.GetChild(4).GetComponent<Renderer>();
		renderer.material.SetColor("_Color", _active ? _primaryColor : Color.black);
	}
}
