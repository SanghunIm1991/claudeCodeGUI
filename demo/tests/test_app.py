import unittest

from gui.app import get_greeting_message


class TestGetGreetingMessage(unittest.TestCase):
    def test_returns_hello_world(self) -> None:
        self.assertEqual(get_greeting_message(), "Hello, World!")


if __name__ == "__main__":
    unittest.main()
