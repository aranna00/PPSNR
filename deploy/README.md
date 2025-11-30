Deployment notes

- Create Docker secrets on the remote host for sensitive values. Example:

  docker secret create twitch_client_id -
  docker secret create twitch_client_secret -
  docker secret create db_connection -

  Each command reads the secret from stdin. On the remote host run:

  echo "my-client-id" | docker secret create twitch_client_id -
  echo "my-client-secret" | docker secret create twitch_client_secret -
  echo "Data Source=/data/ppsnr.db" | docker secret create db_connection -

- Create the deployment directory (the path configured in CI secret `DEPLOY_PATH`) and copy `docker-compose.yml` there. The GitHub Action will do this for you if configured.

- Ensure the volume directory exists and is writable by the container user. If you want the container to run as a specific UID, set `APP_UID` build-arg when building the image.

  mkdir -p /var/lib/ppsnr/data
  chown 1000:1000 /var/lib/ppsnr/data

- Reverse proxy is expected to route public traffic to the container port (8080). Use the reverse proxy to handle TLS and host header routing.

- To update the service manually on the remote host:

  cd /path/to/deploy
  docker compose pull ppsnr-server
  docker compose up -d --remove-orphans --build
