# Linux 生产环境配置

认证配置必须放在发布目录之外。若部署时缺少生产签名密钥，应用会在启动阶段失败，并将明确错误写入服务日志。

创建 `/etc/homemind/homemind.env`，将权限设置为 `600`，且仅允许服务账号访问：

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5280
ConnectionStrings__HomeMind=Server=127.0.0.1;Port=3306;Database=nexus_mind;User=homemind;Password=replace-me;SslMode=Preferred;
Auth__SigningKey=replace-with-a-random-secret-of-at-least-32-bytes
Auth__AccessTokenMinutes=15
Auth__RefreshTokenDays=30
```

只生成一次签名密钥，并在正常发布过程中保持不变。更换密钥会立即使所有现有访问令牌失效。

```bash
openssl rand -base64 48
```

`/etc/systemd/system/homemind.service` 示例：

```ini
[Unit]
Description=HomeMind API
After=network.target

[Service]
WorkingDirectory=/opt/homemind
ExecStart=/usr/bin/dotnet /opt/homemind/HomeMind.Api.dll
EnvironmentFile=/etc/homemind/homemind.env
Restart=on-failure
RestartSec=5
User=homemind
Group=homemind

[Install]
WantedBy=multi-user.target
```

修改环境变量文件或部署新版本后，执行：

```bash
sudo systemctl daemon-reload
sudo systemctl restart homemind
sudo systemctl status homemind --no-pager
sudo journalctl -u homemind -n 100 --no-pager
```

不要将生产密钥放入 `appsettings.json`、源代码仓库或发布压缩包。将
`/etc/homemind/homemind.env` 保持在 `/opt/homemind` 之外，避免发布或回滚时覆盖它。
