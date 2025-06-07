# Aprendendo-Skyrim-Prot-tipo
Quero aprender unity então criei esse protipo de skyrim
diff --git a/README.md b/README.md
index 694e6e58f3fce2211d1f5f1dbd2132ca4cc3d30c..7776b08bb75a4ce8c93b8db5b53ded0eb5e20126 100644
--- a/README.md
+++ b/README.md
@@ -1 +1,62 @@
-# Aprendendo-Skyrim-Prot-tipo
+# Aprendendo Skyrim Protótipo
+
+Este repositório contém um esqueleto de projeto para aprender Unity criando um pequeno protótipo inspirado em **Skyrim**.
+
+## Estrutura do projeto
+
+```
+Assets/
+├── Prefabs/
+├── Scenes/
+└── Scripts/
+```
+
+- **Prefabs**: objetos reutilizáveis para instanciar durante o jogo.
+- **Scenes**: cenas do Unity, como a cena principal do protótipo.
+- **Scripts**: scripts em C# que controlam a lógica do jogo.
+
+## Começando
+
+1. Instale o [Unity Hub](https://unity.com/download) e uma versão do Unity LTS.
+2. Abra o Unity Hub e crie um novo projeto 3D vazio dentro desta pasta.
+3. Copie ou crie suas cenas dentro de `Assets/Scenes` e adicione os scripts que precisar em `Assets/Scripts`.
+4. Importe pacotes ou modelos que desejar para montar o ambiente.
+
+## Exemplo de script
+
+Há um script simples em `Assets/Scripts/SimplePlayerController.cs` para movimentação básica em terceira pessoa:
+
+```csharp
+using UnityEngine;
+
+public class SimplePlayerController : MonoBehaviour
+{
+    public float speed = 5f;
+    public float rotationSpeed = 180f;
+
+    void Update()
+    {
+        float horizontal = Input.GetAxis("Horizontal");
+        float vertical = Input.GetAxis("Vertical");
+
+        Vector3 direction = new Vector3(horizontal, 0f, vertical);
+        if (direction.magnitude > 0.1f)
+        {
+            transform.Translate(direction.normalized * speed * Time.deltaTime, Space.World);
+            Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
+            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
+        }
+    }
+}
+```
+
+Esse script pode ser associado a um GameObject para permitir movimentação usando as teclas **WASD** ou setas.
+
+## Próximos passos
+
+- Criar terrenos e estruturas na cena principal.
+- Implementar coleta de itens e inventário.
+- Adicionar inimigos simples com IA básica.
+- Testar e refinar conforme aprende.
+
+Bom desenvolvimento!
