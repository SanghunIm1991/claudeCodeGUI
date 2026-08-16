import tkinter as tk


def get_greeting_message() -> str:
    """表示する挨拶文字列を返す純粋関数。"""
    return "Hello, World!"


class HelloWorldApp(tk.Tk):
    """Hello World を表示するメインウィンドウ。"""

    WINDOW_TITLE = "Hello World App"
    WINDOW_SIZE = "300x120"

    def __init__(self) -> None:
        super().__init__()
        self.title(self.WINDOW_TITLE)
        self.geometry(self.WINDOW_SIZE)
        self._build_widgets()

    def _build_widgets(self) -> None:
        label = tk.Label(self, text=get_greeting_message(), font=("Arial", 16))
        label.pack(expand=True)

    def run(self) -> None:
        self.mainloop()
