# Translator

Translator - учебное WPF-приложение для трансляции ST-кода в IL и LD.

Программа принимает исходный ST-код, проверяет его и формирует результат трансляции:

- в IL-код;
- в LD-код в формате экспорта CoDeSys 2.3.

Исходный ST-код должен быть написан в LTL-ориентированном стиле спецификаций. Поддерживается ограниченное подмножество ST: булевы выражения, прямые присваивания, конструкции `IF ... THEN ... ELSIF ... END_IF;` и псевдооператорные присваивания вида `_V := V;`.

Приложение поддерживает загрузку исходных файлов `.txt` и `.exp`, а результат трансляции можно сохранить в `.txt` или `.exp`.

---------------------------------------------------------------------------

Translator is an educational WPF application for translating ST code into IL and LD.

The application takes source ST code, validates it and generates a translation result:

- IL code;
- LD code in the CoDeSys 2.3 export format.

The source ST code must be written in an LTL-oriented specification style. The supported ST subset is limited to boolean expressions, direct assignments, `IF ... THEN ... ELSIF ... END_IF;` constructs and pseudo-operator assignments such as `_V := V;`.

The application supports loading `.txt` and `.exp` source files. The translation result can be saved as `.txt` or `.exp`.
